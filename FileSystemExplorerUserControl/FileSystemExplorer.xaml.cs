#region Using statements
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Globalization;
#endregion

namespace FileSystemExplorerUserControl
{
    #region Documentation
    /// <summary>
    /// The basics for the custom-styled treeview is based on the code sample presented in the article entitled
    /// "A Simple WPF Explorer Tree", by Sacha Barber (2007): 
    /// http://www.codeproject.com/Articles/21248/A-Simple-WPF-Explorer-Tree
    /// </summary>
    #endregion

    #region FileSystemExplorer Class
    public partial class FileSystemExplorer : UserControl
    {
        #region Constant/Static Values
        private static string[] m_defaultFileFilter = 
        { 
            ".fts", 
            ".fit", 
            ".fits", 
            ".jpg",
            ".jpeg",
            ".png", 
            ".tif",
            ".tiff",
            ".gif",
            ".bmp"
        };

        private static string[] m_specialToLevelFolders =
        {
            "computer",
            "desktop",
            "my pictures",
            "my documents",
            "favorites"
        };

        private static string SPECIAL_FOLDER_MYCOMPUTER = "computer";
        private static string SPECIAL_FOLDER_FAVORITES = "favorites";
        #endregion

        #region Private Members
        private delegate void GetLogicalDrives();       // Delegate used to run the thread that gets all the logical drives in the system
        private delegate void GetFiles(string path);    // Delegate used to get all the files in a particular folder
        private GetLogicalDrives m_getLogicalDrivesOp;  // Instance of the GetTopLevelItems delegate
        private GetFiles m_getFilesOp;                  // Instance of the GetFiles delegate
        private string m_selectedFile;                  // Holds the currently selected filename (can be null)
        private string m_selectedDirectory;             // Holds the currently selected path
        private string m_currentFilesDirectory;         // Holds the current directory that contains files
        private List<string> m_fileFilter;              // List of filters (".ext") used to include only specified file types
        private List<ThumbnailInfo> m_thumbnailList;    // Holds a list of thumbnail info
        private bool m_getFilesOpInProgress;            // Flags if we're in the process of getting a list of files
        private bool m_getLogicalDrivesOpInProgress;    // Flags if we're getting the special top-level folders
        #endregion

        #region Public Events
        public delegate void FileSelected(object sender, string e);     // Delegate used to fire the FileSelectionChanged event
        public delegate void DirSelected(object sender, string e);      // Delegate used to fire the DirSelectionChanged event
        public event FileSelected FileSelectionChanged;                 // The FileSelectionChanged event
        public event DirSelected DirSelectionChanged;                   // The DirSelectionChanged event
        #endregion

        #region Properties
        public List<ThumbnailInfo> ThumbnailInfoList
        {
            get { return m_thumbnailList; }
        }

        public string SelectedDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(m_selectedDirectory))
                    return "";
                else
                    return m_selectedDirectory;
            }
        }

        public string SelectedFile
        {
            get
            {
                if (string.IsNullOrEmpty(m_selectedFile))
                    return "";
                else 
                    return m_selectedFile;
            }
        }

        public string SelectedFileFullSpec
        {
            get
            {
                string filespec = "";
                if (string.IsNullOrEmpty(m_selectedDirectory) || string.IsNullOrEmpty(m_selectedFile))
                    return filespec;
                else
                {
                    if (m_selectedDirectory.EndsWith(@"\"))
                        filespec = m_selectedDirectory;
                    else
                        filespec = m_selectedDirectory + @"\";

                    if (m_selectedFile.StartsWith(@"\"))
                        filespec += m_selectedFile.Substring(1);
                    else
                        filespec += m_selectedFile;

                    return filespec;
                }
            }
        }

        public List<string> FileFilter
        {
            get { return m_fileFilter; }
            set { m_fileFilter = value; }
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// Convert the logical path ("My Computer\C:\My Images\Image01.jpg") to a physical path ("C:\My Images\Image01.jpg")
        /// </summary>
        /// <param name="logicalPath">The logical path to convert</param>
        /// <returns>Returns the physical path (e.g. a 'real' file system location)</returns>
        public static string GetPhysicalPath(string logicalPath)
        {
            string physicalPath = logicalPath;

            if (string.IsNullOrEmpty(logicalPath))
                return physicalPath;

            try
            {
                string[] pathSegments = logicalPath.Split('\\');
                int pathSegmentIndex = 0;

                // Look for the drive spec... (x:) and disgard everything to the left of the drive letter
                foreach (string pathSegment in pathSegments)
                {
                    if (pathSegment.Contains(':'))
                    {
                        physicalPath = string.Join("\\", pathSegments, pathSegmentIndex, pathSegments.Length - pathSegmentIndex);
                        break;
                    }

                    pathSegmentIndex++;
                }
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FileSystemExplorer.GetPhysicalPath: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                physicalPath = logicalPath;
            }

            return physicalPath;
        }
        #endregion

        #region Construction
        public FileSystemExplorer()
        {
            InitializeComponent();

            m_currentFilesDirectory = "";
            m_selectedDirectory = null;  // No dir selected yet
            m_selectedFile = null;  // Not file selected
            m_fileFilter = new List<string>(m_defaultFileFilter);  // Setup the default file filters (.jpg, .fit, etc.)
            m_thumbnailList = new List<ThumbnailInfo>();  // Create the list that holds thumbnail info on files

            GetSpecialFolders();
        }
        #endregion

        #region TreeView Methods
        public void GotoFolder(string path)
        {
            if (!Directory.Exists(path))
                return;

            WalkTreeToDirectory(path);
        }

        private void GetSpecialFolders()
        {
            TreeViewItem specialFolder;
            string favsPath = "";

            try
            {
                // Construct the path to the Favorites directory (there's no SpecialFolder value for it)
                favsPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                int i = favsPath.IndexOf(@"\Desktop");
                favsPath = favsPath.Substring(0, i) + @"\Links";
                
                // Does the directory actually exist (it's only in Vista/Win7/8)?
                if (Directory.Exists(favsPath))
                {
                    specialFolder = new TreeViewItem();
                    specialFolder.Header = "Favorites";
                    specialFolder.Tag = favsPath;
                    specialFolder.FontWeight = FontWeights.Normal;
                    specialFolder.Items.Add(null);  // Add a placeholder 
                    treeViewDirectories.Items.Add(specialFolder);
                }
            }
            catch
            {
            }

            specialFolder = new TreeViewItem();
            specialFolder.Header = "Desktop";
            specialFolder.Tag = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            specialFolder.FontWeight = FontWeights.Normal;
            specialFolder.Items.Add(null);  // Add a placeholder 
            treeViewDirectories.Items.Add(specialFolder);

            specialFolder = new TreeViewItem();
            specialFolder.Header = "My Documents";
            specialFolder.Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            specialFolder.FontWeight = FontWeights.Normal;
            specialFolder.Items.Add(null);  // Add a placeholder 
            treeViewDirectories.Items.Add(specialFolder);

            specialFolder = new TreeViewItem();
            specialFolder.Header = "My Pictures";
            specialFolder.Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            specialFolder.FontWeight = FontWeights.Normal;
            specialFolder.Items.Add(null);  // Add a placeholder 
            treeViewDirectories.Items.Add(specialFolder);

            TreeViewItem myComputer;
            myComputer = new TreeViewItem();
            myComputer.Header = "Computer";
            myComputer.Tag = "Computer";
            myComputer.FontWeight = FontWeights.Normal;
            myComputer.Items.Add(null);  // Add a placeholder 
            treeViewDirectories.Items.Add(myComputer);
        }

        private void DoGetLogicalDrives()
        {
            if (m_getLogicalDrivesOpInProgress)
                return;

            m_getLogicalDrivesOpInProgress = true;

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                try
                {
                    // Find the "Computer" folder...
                    TreeViewItem myComputer = null;
                    foreach (TreeViewItem tvi in treeViewDirectories.Items)
                    {
                        if (tvi.Header.ToString().ToLower().CompareTo(SPECIAL_FOLDER_MYCOMPUTER) == 0)
                        {
                            myComputer = tvi;
                            break;
                        }
                    }

                    if (myComputer == null)
                        return;  // Should never happen

                    // Get the top-level logical drives on the system and add them to "Computer"...
                    TreeViewItem newFolder;
                    foreach (string logicalDrive in Directory.GetLogicalDrives())
                    {
                        newFolder = new TreeViewItem();
                        newFolder.Header = logicalDrive;
                        newFolder.Tag = logicalDrive;
                        newFolder.FontWeight = FontWeights.Normal;
                        newFolder.Items.Add(null);  // Add a placeholder (this will be filled if the user opens the node)
                        myComputer.Items.Add(newFolder);
                    }
                }
                catch (Exception ex)
                {
                    SharedUtil.SharedEventLog.Log("FileSystemExplorer.DoGetLogicalDrives: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                }
            }));
        }

        private void DoGetLogicalDrives4TreeWalk()
        {
            try
            {
                // Find the "Computer" folder...
                TreeViewItem myComputer = null;
                foreach (TreeViewItem tvi in treeViewDirectories.Items)
                {
                    if (tvi.Header.ToString().ToLower().CompareTo(SPECIAL_FOLDER_MYCOMPUTER) == 0)
                    {
                        myComputer = tvi;
                        break;
                    }
                }

                if (myComputer == null)
                    return;  // Should never happen

                // Get the top-level logical drives on the system and add them to "Computer"...
                TreeViewItem newFolder;
                foreach (string logicalDrive in Directory.GetLogicalDrives())
                {
                    newFolder = new TreeViewItem();
                    newFolder.Header = logicalDrive;
                    newFolder.Tag = logicalDrive;
                    newFolder.FontWeight = FontWeights.Normal;
                    newFolder.Items.Add(null);  // Add a placeholder (this will be filled if the user opens the node)
                    myComputer.Items.Add(newFolder);
                }
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FileSystemExplorer.DoGetLogicalDrives4TreeWalk: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }

        private void DoGetLogicalDrivesComplete(IAsyncResult result) 
        {
            m_getLogicalDrivesOpInProgress = false;
        }

        private void DoGetFiles(string path)
        {
            if (m_getFilesOpInProgress || string.IsNullOrEmpty(path))
                return;

            // Stops us get the contents of the same directory more than once...
            if (!string.IsNullOrEmpty(m_currentFilesDirectory) && m_currentFilesDirectory.CompareTo(m_selectedDirectory) == 0)
                return;

            m_currentFilesDirectory = path;
            m_getFilesOpInProgress = true;

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                try
                {
                    // Get all the files that fit the filter for this folder...
                    System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(path);  // Dir to search from

                    listBoxFiles.Items.Clear();
                    m_thumbnailList = new List<ThumbnailInfo>();  // Always create a new list because simply Clear()ing the list does not trigger the DataContext to rebind

                    if (!m_specialToLevelFolders.Contains(path.ToLower()))  // If it's not a special top-level folder, get the list of files
                    {
                        foreach (System.IO.FileInfo fi in di.GetFiles())
                        {
                            if (m_fileFilter.Contains(fi.Extension.ToLower()))
                            {
                                if (!IsInList(listBoxFiles.Items, fi.Name))  // Add the file, if it's not already in the list (shouldn't be)
                                {
                                    listBoxFiles.Items.Add(fi.Name);
                                    m_thumbnailList.Add(new ThumbnailInfo(fi.FullName));
                                }
                            }
                        }
                    }

                    // Fire the directory changed event now that we've collected the file and place-holder thumbnail info
                    OnDirSelectionChanged(m_selectedDirectory);
                }
                catch (Exception ex)
                {
                    SharedUtil.SharedEventLog.Log("FileSystemExplorer.DoGetFiles: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                }
            }));
        }

        private void DoGetFilesComplete(IAsyncResult result) 
        {
            m_getFilesOpInProgress = false;
        }

        private void WalkTreeToDirectory(string path) 
        {
            if (string.IsNullOrEmpty(path))
                return;

            string[] pathSegments = path.Split('\\');
            pathSegments[0] += @"\";  // Add a "\" to the drive spec
            int pathSegmentIndex = 0;
            TreeViewItem tvi = null;

            foreach(string pathSegment in pathSegments)
            {
                tvi = SearchTree(pathSegments[pathSegmentIndex], tvi);
                if(tvi == null)
                    return;  // Couldn't find the path segment - should never happen...

                tvi.IsSelected = true;
                tvi.IsExpanded = true;
                DoSyncFolder4TreeWalk(tvi);
                tvi.BringIntoView();
                pathSegmentIndex++;
            }

            OnDirSelectionChanged(path);
        }

        private TreeViewItem SearchTree(string searchItem, TreeViewItem startItem)
        {
            if (startItem == null)
            {
                foreach (TreeViewItem tvi in treeViewDirectories.Items)
                {
                    if (tvi.Header.ToString().ToLower().CompareTo("computer") == 0)
                    {
                        tvi.IsSelected = true;  // Select and ...
                        tvi.IsExpanded = true;  // ... expand My Computer
                        DoSyncFolder4TreeWalk(tvi);
                        tvi.BringIntoView();
                        return SearchTree(searchItem, tvi);  // Call ourselves to start searching for the root drive (*not* the tree root, which is Desktop, My Computer, etc.)
                    }
                }
            }
            else
            {
                // Search starting at the provided start-point
                foreach (TreeViewItem tvi in startItem.Items)
                {
                    if (searchItem.CompareTo(tvi.Header.ToString()) == 0)
                        return tvi;  // Found a match
                }
            }

            return null;
        }

        private void DoSyncFolder4TreeWalk(TreeViewItem selectedFolder)
        {
            TreeViewItem newSubFolder;

            m_selectedDirectory = selectedFolder.Tag.ToString();

            selectedFolder.Items.Clear();
            try
            {
                // Get the files in the newly expanded/selected folder...
                if (m_selectedDirectory.ToLower().CompareTo(SPECIAL_FOLDER_MYCOMPUTER) == 0)
                {
                    // Get the top-level list of drives...
                    DoGetLogicalDrives4TreeWalk();
                }
                else
                {
                    // Get any sub-directories
                    DirectoryInfo di;
                    foreach (string folder in Directory.GetDirectories(m_selectedDirectory))
                    {
                        di = new DirectoryInfo(folder);
                        FileAttributes fi = di.Attributes;
                        if (((fi & FileAttributes.Hidden) == FileAttributes.Hidden) ||
                            ((fi & FileAttributes.System) == FileAttributes.System))
                            continue;

                        newSubFolder = new TreeViewItem();
                        newSubFolder.Header = folder.Substring(folder.LastIndexOf("\\") + 1);  // The folder name
                        newSubFolder.Tag = folder;  // Full path
                        newSubFolder.FontWeight = FontWeights.Normal;
                        newSubFolder.Items.Add(null);  // Add a placeholder (this will be filled if the user opens the node)
                        selectedFolder.Items.Add(newSubFolder);
                    }
                }
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FileSystemExplorer.DoSyncFolder4TreeWalk: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }

        private void ProcessFolderSelectionOrExpansion(TreeViewItem selectedFolder)
        {
            TreeViewItem newSubFolder;

            m_selectedDirectory = selectedFolder.Tag.ToString();

            selectedFolder.Items.Clear();
            try
            {
                // Start a new thread to get the files in the newly expanded/selected folder...
                if (m_selectedDirectory.ToLower().CompareTo(SPECIAL_FOLDER_MYCOMPUTER) == 0)
                {
                    // Kick-off a new thread to get the top-level list of drives...
                    m_getLogicalDrivesOp = new GetLogicalDrives(DoGetLogicalDrives);
                    m_getLogicalDrivesOp.BeginInvoke(DoGetLogicalDrivesComplete, null);
                }
                else 
                {
                    m_getFilesOp = new GetFiles(DoGetFiles);
                    m_getFilesOp.BeginInvoke(m_selectedDirectory, DoGetFilesComplete, null);  // The full path is in selectedFolder.Tag

                    // Get any sub-directories
                    DirectoryInfo di;
                    foreach (string folder in Directory.GetDirectories(m_selectedDirectory))
                    {
                        di = new DirectoryInfo(folder);
                        FileAttributes fi = di.Attributes;
                        if (((fi & FileAttributes.Hidden) == FileAttributes.Hidden) ||
                            ((fi & FileAttributes.System) == FileAttributes.System))
                            continue;

                        newSubFolder = new TreeViewItem();
                        newSubFolder.Header = folder.Substring(folder.LastIndexOf("\\") + 1);  // The folder name
                        newSubFolder.Tag = folder;  // Full path
                        newSubFolder.FontWeight = FontWeights.Normal;
                        newSubFolder.Items.Add(null);  // Add a placeholder (this will be filled if the user opens the node)
                        selectedFolder.Items.Add(newSubFolder);
                    }

                    // Get any shortcuts if this is the favorites special folder...
                    if (selectedFolder.Header.ToString().ToLower().CompareTo(SPECIAL_FOLDER_FAVORITES) == 0)
                    {
                        string shortcutName;
                        foreach (string shortcut in Directory.GetFiles(m_selectedDirectory, "*.lnk"))
                        {
                            shortcutName = shortcut.Substring(shortcut.LastIndexOf("\\") + 1);
                            shortcutName = shortcutName.Substring(0, shortcutName.Length - 4);

                            // The only way to get the target of a link seems to be via the Windows Script Host COM object...
                            IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                            IWshRuntimeLibrary.IWshShortcut link = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcut);

                            newSubFolder = new TreeViewItem();
                            newSubFolder.Header = shortcutName;
                            newSubFolder.Tag = link.TargetPath;  // Full path target of the link
                            newSubFolder.FontWeight = FontWeights.Normal;
                            newSubFolder.Items.Add(null);  // Add a placeholder (this will be filled if the user opens the node)
                            selectedFolder.Items.Add(newSubFolder);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FileSystemExplorer.ProcessFolderSelectionOrExpansion: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }

        private bool IsInList(ItemCollection list, string searchItem)
        {
            // This method is necessary as the standard Contains() method on the listbox doesn't work reliably for some reason
            foreach (string s in list)
            {
                if (s.CompareTo(searchItem) == 0)
                    return true;
            }
            return false;
        }
        #endregion

        #region TreeView Event Handlers
        private void treeViewDirectories_Selected(object sender, RoutedEventArgs e)
        {
            ProcessFolderSelectionOrExpansion((TreeViewItem)e.OriginalSource);
        }

        private void treeViewDirectories_Expanded(object sender, RoutedEventArgs e)
        {
            ProcessFolderSelectionOrExpansion((TreeViewItem)e.OriginalSource);
        }
        #endregion

        #region Listbox Files Event Handlers
        private void listBoxFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && ((DataObject)e.Data).ContainsFileDropList())
            {
                e.Effects = DragDropEffects.Copy;
            }

            OnDragEnter(e);  // Re-raise the event
        }

        private void listBoxFiles_Drop(object sender, DragEventArgs e)
        {
            string path = "";
            listBoxFiles.Items.Clear();

            try
            {
                if (e.Data is DataObject && ((DataObject)e.Data).ContainsFileDropList())
                {
                    foreach (string file in ((DataObject)e.Data).GetFileDropList())
                    {
                        // Is this a file or a path?
                        if (System.IO.Directory.Exists(file))
                        {
                            // It's a directory - sync our treeview to the directory (this will later force display of files in the dir)
                            path = file;
                            break;
                        }
                        else
                        {
                            // Extract the dir from the first filename in the dropped collection
                            string tmpPath = ((DataObject)e.Data).GetFileDropList()[0];
                            int i = tmpPath.LastIndexOf(@"\");
                            if (i == -1)
                                return;  // Should never happen with a well-formed filespec

                            path = tmpPath.Substring(0, i);
                        }
                    }

                    // Sync the TreeView to the directory containing the files that were just dropped on us...
                    WalkTreeToDirectory(path);
                }
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FileSystemExplorer.listBoxFiles_Drop: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }

            OnDrop(e);  // Re-raise the event
        }

        private void listBoxFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listBoxFiles.SelectedValue == null)
                return;

            m_selectedFile = listBoxFiles.SelectedValue.ToString();
            OnFileSelectionChanged(m_selectedFile);
        }
        #endregion

        #region Raise Events
        protected void OnFileSelectionChanged(string selectedFile)
        {
            if (FileSelectionChanged != null && !string.IsNullOrEmpty(selectedFile))
                FileSelectionChanged(this, selectedFile);
        }

        protected void OnDirSelectionChanged(string selectedDir)
        {
            if (DirSelectionChanged != null && !string.IsNullOrEmpty(selectedDir))
                DirSelectionChanged(this, selectedDir);
        }
        #endregion
    }
    #endregion

    #region HeaderToImageConverter Class
    [ValueConversion(typeof(string), typeof(bool))]
    public class HeaderToImageConverter : IValueConverter
    {
        public static HeaderToImageConverter Instance = new HeaderToImageConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;

            string sVal = value.ToString();

            if (sVal.Contains(@"\"))
            {
                // Notice the syntax for referencing our resources:
                // "pack://application:,,,/AssemblyName;component/PathToResource/resourceFileName.extn"
                // By explicitly referencing the assembly for our user control, the host application
                // can also find the resources
                Uri uri = new Uri("pack://application:,,,/FileSystemExplorerUserControl;component/Resources/diskdrive.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
            else if (sVal.StartsWith("Favorites"))
            {
                Uri uri = new Uri("pack://application:,,,/FileSystemExplorerUserControl;component/Resources/Favs.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
            else if(sVal.StartsWith("Desktop"))
            {
                Uri uri = new Uri("pack://application:,,,/FileSystemExplorerUserControl;component/Resources/Desktop.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
            else if (sVal.StartsWith("Computer"))
            {
                Uri uri = new Uri("pack://application:,,,/FileSystemExplorerUserControl;component/Resources/MyComputer.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
            else if (sVal.StartsWith("My Documents"))
            {
                Uri uri = new Uri("pack://application:,,,/FileSystemExplorerUserControl;component/Resources/MyDocuments.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
            else if (sVal.StartsWith("My Pictures"))
            {
                Uri uri = new Uri("pack://application:,,,/FileSystemExplorerUserControl;component/Resources/MyPictures.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
            else
            {
                Uri uri = new Uri("pack://application:,,,/FileSystemExplorerUserControl;component/Resources/folder.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
    #endregion
}
