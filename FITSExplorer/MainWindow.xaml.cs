#region Using Statements
using System;
using System.Collections;
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
using System.Diagnostics;
using FITSFramework;
using FileSystemExplorerUserControl;
#endregion

namespace FITSExplorer
{
    public partial class MainWindow : Window
    {
        #region Private enums
        private enum ExportOptions { ExportAsText, ExportAsCSV, ExportAsExcelSheet };
        #endregion

        #region Private Members
        private FITSFramework.FITSFile m_fitsFile;          // The current FITS file
        private bool m_blackPointInit = false;              // The value of the image's FITS stretch black point
        private bool m_whitePointInit = false;              // The value of the image's FITS stretch white point
        private bool m_isFullScreen;                        // Toggle full screen mode
        private bool m_isPreviewMaximized;                  // Toggle the preview pane 
        private bool m_isFileSystemMaximized;               // Toggle the file system pane
        private bool m_isFITSHeaderMaximized;               // Toggle the FITS Header pane
        private bool m_createThumbnailsOpRunning;           // Used to flag if the thread is running
        private Action m_createThumbnailsOp;                // Used to create thumbnails
        private WindowStyle m_mainWndStyle;                 // Used to save a copy of the main window's style (used with full-screen toggle)
        private WindowState m_mainWndState;                 // Remembers the main window state when toggling full-screen
        private GridLength m_gridColumnDefWidth;            // Remembers the size of the left pane (used when toggling max preview pane)
        private GridLength m_gridRowDefHeight;              // Remembers the size of the top pane (used when toggling max file system pane)
        private double m_previewRotationAngle;              // Used when rotating the preview image
        #endregion

        #region Construction
        public MainWindow()
        {
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            m_isFullScreen = false;
            m_isPreviewMaximized = false;
            m_isFileSystemMaximized = false;
            m_isFITSHeaderMaximized = false;
            m_previewRotationAngle = 0;

            sliderScreenStretchBlack.Visibility = System.Windows.Visibility.Hidden;
            sliderScreenStretchWhite.Visibility = System.Windows.Visibility.Hidden;
            sliderThumbnailSize.Visibility = System.Windows.Visibility.Hidden;

            // Setup delegates...
            m_createThumbnailsOp = CreateThumbnails;

            // Set the saved main window position and dimensions...
            this.Height = Properties.Settings.Default.Window_Height;
            this.Left = Properties.Settings.Default.Window_Left;
            this.Top = Properties.Settings.Default.Window_Top;
            this.Width = Properties.Settings.Default.Window_Width;
            this.WindowState = Properties.Settings.Default.Window_State;

            // Hook into various custom event raised by the file system explorer user control...
            fileSystemExplorer.FileSelectionChanged += new FileSystemExplorer.FileSelected(fileSystemExplorer_FileSelectionChanged);
            fileSystemExplorer.DirSelectionChanged += new FileSystemExplorer.DirSelected(fileSystemExplorer_DirSelectionChanged);

            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastUsedFolder))
                fileSystemExplorer.GotoFolder(Properties.Settings.Default.LastUsedFolder);

            InitHotKeys();
        }

        private void InitHotKeys()
        {
            // Create global 'hotkey' (space and shift+space) to enable cycling forward/backward between the file, thumbnail and preview panes...
            KeyGesture paneKeyGesture;
            InputBinding paneKeyGestureBinding;
            CommandBinding paneKeyGestureCommand;

            // Space (toggle thumbnail/preview pane)
            paneKeyGesture = new KeyGesture(Key.Space, ModifierKeys.None);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.BrowseForward, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.BrowseForward);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(TogglePreview);
            CommandBindings.Add(paneKeyGestureCommand);

            // Alt+F (Full-Screen - toggle full-screen)
            paneKeyGesture = new KeyGesture(Key.F, ModifierKeys.Alt);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.Zoom, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.Zoom);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(ToggleFullScreen);
            CommandBindings.Add(paneKeyGestureCommand);

            // Esc (Exit Full-Screen)
            paneKeyGesture = new KeyGesture(Key.Escape, ModifierKeys.None);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.BrowseHome, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.BrowseHome);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(ExitFullScreen);
            CommandBindings.Add(paneKeyGestureCommand);

            // Alt+P (Preview - maximize thumbnail/preview pane toggle)
            paneKeyGesture = new KeyGesture(Key.P, ModifierKeys.Alt);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.IncreaseZoom, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.IncreaseZoom);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(MaximizePreview);
            CommandBindings.Add(paneKeyGestureCommand);

            // Alt+I (FIle System - maximize file system pane toggle)
            paneKeyGesture = new KeyGesture(Key.I, ModifierKeys.Alt);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.FirstPage, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.FirstPage);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(ToggleFileSystem);
            CommandBindings.Add(paneKeyGestureCommand);

            // Alt+H (FITS Header - maximize FITS header pane toggle)
            paneKeyGesture = new KeyGesture(Key.H, ModifierKeys.Alt);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.LastPage, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.LastPage);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(ToggleFITSHeader);
            CommandBindings.Add(paneKeyGestureCommand);

            // Alt+R (Rotate image right 90 degrees)
            paneKeyGesture = new KeyGesture(Key.R, ModifierKeys.Alt);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.NextPage, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.NextPage);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(Menu_Preview_Rotate90Right_Click);
            CommandBindings.Add(paneKeyGestureCommand);

            // Alt+L (Rotate image left 90 degrees)
            paneKeyGesture = new KeyGesture(Key.L, ModifierKeys.Alt);
            paneKeyGestureBinding = new InputBinding(NavigationCommands.PreviousPage, paneKeyGesture);
            InputBindings.Add(paneKeyGestureBinding);
            paneKeyGestureCommand = new CommandBinding(NavigationCommands.PreviousPage);
            paneKeyGestureCommand.Executed += new ExecutedRoutedEventHandler(Menu_Preview_Rotate90Left_Click);
            CommandBindings.Add(paneKeyGestureCommand);

            // Save the window style and other properties for later...
            m_mainWndStyle = this.WindowStyle;
            m_mainWndState = this.WindowState;

            m_gridColumnDefWidth = gridColumnDefLeftPane.Width;
            m_gridRowDefHeight = gridRowDefTopPane.Height;
        }
        #endregion 

        #region Windows and Control-related Event Handlers
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save the main window position and dimensions...
            Properties.Settings.Default.Window_Height = this.Height;
            Properties.Settings.Default.Window_Left = this.Left;
            Properties.Settings.Default.Window_Top = this.Top;
            Properties.Settings.Default.Window_Width = this.Width;
            Properties.Settings.Default.Window_State = this.WindowState;

            Properties.Settings.Default.LastUsedFolder = fileSystemExplorer.SelectedDirectory;

            Properties.Settings.Default.Save();
        }

        private void sliderScreenStretchBlack_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!m_blackPointInit)
            {
                m_blackPointInit = true;
                return;
            }

            int blackPoint, whitePoint;
            blackPoint = (int)Math.Round(sliderScreenStretchBlack.Value, 0);
            whitePoint = (int)Math.Round(sliderScreenStretchWhite.Value, 0);

            sliderScreenStretchBlack.ToolTip = blackPoint.ToString();
            if (m_fitsFile != null && m_fitsFile.IsValidFITSFile)
            {
                if(m_fitsFile.StretchImage(FITSFramework.FITSFile.ImageStreamTo.InMemoryImage, blackPoint, whitePoint))
                    imagePreview.Source = m_fitsFile.ImageSource;
            }
        }

        private void sliderScreenStretchWhite_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!m_whitePointInit)
            {
                m_whitePointInit = true;
                return;
            }

            int blackPoint, whitePoint;
            blackPoint = (int)Math.Round(sliderScreenStretchBlack.Value, 0);
            whitePoint = (int)Math.Round(sliderScreenStretchWhite.Value, 0);

            sliderScreenStretchWhite.ToolTip = whitePoint.ToString();
            if (m_fitsFile != null && m_fitsFile.IsValidFITSFile)
            {
                if(m_fitsFile.StretchImage(FITSFramework.FITSFile.ImageStreamTo.InMemoryImage, blackPoint, whitePoint))
                    imagePreview.Source = m_fitsFile.ImageSource;
            }
        }

        private void buttonPreferences_Click(object sender, RoutedEventArgs e)
        {
            PreferencesWindow prefs = new PreferencesWindow();
            prefs.Show();
        }

        private void listViewThumbnails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listViewThumbnails.SelectedValue == null)
                return;

            ThumbnailInfo thumbInfo = (ThumbnailInfo)listViewThumbnails.SelectedValue;
            DoPreview(thumbInfo.FullPath);
        }

        private void sliderThumbnailSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                // Get a handle to the custom dynamic resource we created in App.xaml - see App.xaml for more comments
                this.Resources["CustomItemWidthValue"] = (double)e.NewValue;
            }
            catch(Exception ex)
            {
                SharedUtil.SharedEventLog.Log("MainWindow.CreateThumbnail: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }
        #endregion

        #region FileSystemExplorer Event Handlers
        private void CreateThumbnails()
        {
            if(!m_createThumbnailsOpRunning)
                return;

            if (fileSystemExplorer.ThumbnailInfoList == null || fileSystemExplorer.ThumbnailInfoList.Count == 0)
            {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    sliderThumbnailSize.Visibility = System.Windows.Visibility.Hidden;
                }));
                return;
            }

            // Create the list of image previews...
            foreach (ThumbnailInfo tni in fileSystemExplorer.ThumbnailInfoList)
            {
                // Is the main thread trying to stop us in mid-process?
                if (!m_createThumbnailsOpRunning)
                    return;

                CreateThumbnail(tni);
                System.Threading.Thread.Sleep(250); // We need to sleep our thread for a moment or the whole UI will freeze
            }

            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
            {
                sliderThumbnailSize.Visibility = System.Windows.Visibility.Visible; 
            }));
        }

        private void CreateThumbnailsComplete(IAsyncResult result)
        {
            m_createThumbnailsOpRunning = false;  // Flag that the thread has finished
        }

        private void CreateThumbnail(ThumbnailInfo tni)
        {
            Image image;
            FITSFile fitsFile;
            BitmapImage bitmap;

            if (tni.ThumbnailCreated)
                return;  // The thumbnail has already been created previously

            if (FITSFramework.FITSFile.IsFITSImage(tni.Filename))
            {

                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    try
                    {
                        // Create a Windows-displayable image thumbnail from a FITS file...
                        fitsFile = new FITSFramework.FITSFile();
                        image = fitsFile.CreateThumbnail(tni.FullPath);  // Takes about 140ms on a modern machine
                        if (image != null)
                        {
                            tni.ThumbnailImage = image;
                            tni.ThumbnailCreated = true;
                            listViewThumbnails.Items.Add(tni);  // Note that we don't use data binding as it's very slow for images...
                        }
                    }
                    catch (Exception ex)
                    {
                        SharedUtil.SharedEventLog.Log("MainWindow.CreateThumbnail->FITS: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                    }
                }));
            }
            else
            {
                // Create an image from the file...
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)(() =>
                {
                    try
                    {
                        image = new Image();
                        bitmap = new BitmapImage();
                        bitmap.BeginInit();

                        if (TryFindResource("CustomItemWidthValue") != null)
                            bitmap.DecodePixelHeight = (int)(double)TryFindResource("CustomItemWidthValue");
                        else
                            bitmap.DecodePixelHeight = 150;

                        bitmap.UriSource = new Uri(tni.FullPath, UriKind.Absolute);
                        bitmap.EndInit();
                        image.Source = bitmap;
                        tni.ThumbnailImage = image;
                        listViewThumbnails.Items.Add(tni);  // Note that we don't use data binding as it's very slow for images...
                    }
                    catch (Exception ex)
                    {
                        SharedUtil.SharedEventLog.Log("MainWindow.CreateThumbnail->Image: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                    }
                }));
            }
        }

        private void fileSystemExplorer_DirSelectionChanged(object sender, string e)
        {
            if (string.IsNullOrEmpty(fileSystemExplorer.SelectedDirectory))
                return;

            // Are we already getting thumbnails for a directory?
            if (m_createThumbnailsOpRunning)
            {
                // If so, ask the thread to stop...
                m_createThumbnailsOpRunning = false;

                // ... and wait for 100ms for the thread to exit cleanly
                System.Threading.Thread.Sleep(100);
            }

            // Now create the real previews on a background thread...
            listViewThumbnails.Items.Clear();
            m_createThumbnailsOpRunning = true;
            m_createThumbnailsOp.BeginInvoke(CreateThumbnailsComplete, null);
        }

        private void fileSystemExplorer_FileSelectionChanged(object sender, string e)
        {
            DoPreview(FileSystemExplorer.GetPhysicalPath(fileSystemExplorer.SelectedFileFullSpec));
        }
        #endregion

        #region FITS Image Preview Methods
        private void DoFITSPreview(string file)
        {
            m_blackPointInit = false;  // Prevents the image being loaded and then (unecessarily) re-stretched when the
            m_whitePointInit = false;  // stretch sliders are initiated

            m_fitsFile = new FITSFramework.FITSFile(file);
            if (m_fitsFile.ReadHeader())
                listViewFTSHeader.DataContext = m_fitsFile.FITSHeaderItems;  // Bind to the collection of FITS header items
            else
                return;  // Don't try to read the image if we failed with the header

            if (m_fitsFile.ReadImage(FITSFramework.FITSFile.ImageStreamTo.InMemoryImage))
            {
                imagePreview.Source = m_fitsFile.ImageSource;

                if (m_fitsFile.PixelDataType == typeof(byte))
                {
                    sliderScreenStretchBlack.Minimum = byte.MinValue;
                    sliderScreenStretchBlack.Maximum = byte.MaxValue;
                    sliderScreenStretchWhite.Minimum = byte.MinValue;
                    sliderScreenStretchWhite.Maximum = byte.MaxValue;
                }
                else
                {
                    sliderScreenStretchBlack.Minimum = ushort.MinValue;  // We use ushort.MinValue/ushort.MaxValue because data with
                    sliderScreenStretchBlack.Maximum = ushort.MaxValue;  // a bit-depth > unsigned 16-bit is re-sampled to be 
                    sliderScreenStretchWhite.Minimum = ushort.MinValue;  // unsigned 16-bit for the image preview
                    sliderScreenStretchWhite.Maximum = ushort.MaxValue;

                }

                sliderScreenStretchBlack.Value = m_fitsFile.StretchRangeBlack;
                sliderScreenStretchBlack.ToolTip = m_fitsFile.StretchRangeBlack.ToString();

                sliderScreenStretchWhite.Value = m_fitsFile.StretchRangeWhite;
                sliderScreenStretchWhite.ToolTip = m_fitsFile.StretchRangeWhite.ToString();
            }
        }
        #endregion

        #region Non-FITS Image Preview Methods
        private void DoNonFITSPreview(string file)
        {
            try
            {
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(file, UriKind.Absolute);                
                bi.EndInit();

                imagePreview.Source = bi;
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("MainWindow.DoNonFITSPreview: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }
        #endregion

        #region Misc Methods
        private void DoPreview(string file)
        {
            // Make sure the image is initially unrotated (it might have been rotated when previewing the previous image)
            m_previewRotationAngle = 0;
            RotateTransform rotateImage = new RotateTransform(0);
            rotateImage.CenterX = imagePreview.ActualWidth / 2;
            rotateImage.CenterY = imagePreview.ActualHeight / 2;
            imagePreview.RenderTransform = rotateImage;

            sliderThumbnailSize.Visibility = System.Windows.Visibility.Visible;

            if (FITSFile.IsFITSImage(file))
            {
                DoFITSPreview(file);
                sliderScreenStretchBlack.Visibility = System.Windows.Visibility.Visible;
                sliderScreenStretchWhite.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                DoNonFITSPreview(file);
                sliderScreenStretchBlack.Visibility = System.Windows.Visibility.Hidden;
                sliderScreenStretchWhite.Visibility = System.Windows.Visibility.Hidden;
            }

            tabControl.SelectedIndex = 1;  // Select the preview pane
        }

        private void RotateImage(double angle)
        {
            m_previewRotationAngle += angle;
            if (angle > 360 || angle < -360)
                m_previewRotationAngle = 0;

            RotateTransform rotateImage = new RotateTransform(m_previewRotationAngle);
            rotateImage.CenterX = imagePreview.ActualWidth / 2;
            rotateImage.CenterY = imagePreview.ActualHeight / 2;
            imagePreview.RenderTransform = rotateImage;
        }

        private void ExportFITSHeaderData(ExportOptions exportType)
        {
            string path = null;
            string fileExtension = null;

            if (exportType == ExportOptions.ExportAsText)
                fileExtension = ".txt";
            else if (exportType == ExportOptions.ExportAsCSV)
                fileExtension = ".csv";
            else
                fileExtension = ".xls";

            try
            {
                if (m_fitsFile == null || m_fitsFile.FITSHeaderItems == null)
                    return;

                string timestamp = DateTime.Now.ToLongTimeString();
                timestamp = timestamp.Replace(":", "");

                path = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\Temp\" +
                    @"\FITSExplorer-FITSHeader-" + timestamp + fileExtension;

                StringBuilder line = new StringBuilder();
                using (StreamWriter outfile = new StreamWriter(path))
                {
                    if (exportType == ExportOptions.ExportAsCSV)
                    {
                        outfile.WriteLine("Key,Value,Comment");

                        foreach (FITSHeaderItem fhi in m_fitsFile.FITSHeaderItems)
                        {
                            line.Clear();
                            line.Append(fhi.Key);
                            line.Append(",");
                            line.Append(fhi.Value);
                            line.Append(",");
                            line.Append(fhi.Comment);
                            outfile.WriteLine(line.ToString());
                        }
                    }
                    else if (exportType == ExportOptions.ExportAsText)
                    {
                        foreach (FITSHeaderItem fhi in m_fitsFile.FITSHeaderItems)
                        {
                            line.Clear();
                            line.Append(fhi.Key);
                            line.Append(" = ");
                            line.Append(fhi.Value);
                            if (fhi.HasComment)
                            {
                                line.Append(" (");
                                line.Append(fhi.Comment);
                                line.Append(")");
                            }
                            outfile.WriteLine(line.ToString());
                        }
                    }
                    else
                    {
                        // Create an instance of Excel...
                        Microsoft.Office.Interop.Excel.Application excel = new Microsoft.Office.Interop.Excel.Application();  
                        excel.Visible = true;  // ... and show it

                        // Create a new, blank sheet...
                        excel.Workbooks.Add(Microsoft.Office.Interop.Excel.XlWBATemplate.xlWBATWorksheet);  

                        excel.ActiveCell.set_Item(1, 1, "Key");  // Add data to R1C1
                        excel.ActiveCell.set_Item(1, 2, "Value");  // Add data to R1C2
                        excel.ActiveCell.set_Item(1, 3, "Comment");  // Add data to R1C3

                        int rowIndex = 2;
                        foreach (FITSHeaderItem fhi in m_fitsFile.FITSHeaderItems)
                        {
                            excel.ActiveCell.set_Item(rowIndex, 1, fhi.Key);
                            excel.ActiveCell.set_Item(rowIndex, 2, fhi.Value);
                            excel.ActiveCell.set_Item(rowIndex, 3, fhi.Comment);
                            rowIndex++;
                        }
                    }
                }

                // Open text or csv in the associated app...
                if(exportType == ExportOptions.ExportAsText || exportType == ExportOptions.ExportAsCSV)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to export FITS header data", "Error Saving File");
                SharedUtil.SharedEventLog.Log("MainWindow.Menu_Header_ExportText_Click: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }
        #endregion

        #region Hotkey Handlers
        private void TogglePreview(object sender, ExecutedRoutedEventArgs e)
        {
            if (tabControl.SelectedIndex == (tabControl.Items.Count - 1))
                tabControl.SelectedIndex = 0;
            else
                tabControl.SelectedIndex++;
        }

        private void ToggleFullScreen(object sender, ExecutedRoutedEventArgs e)
        {
            m_isFullScreen = !m_isFullScreen;
            if (m_isFullScreen)
            {
                m_mainWndState = this.WindowState;
                this.WindowStyle = System.Windows.WindowStyle.None;
                this.WindowState = System.Windows.WindowState.Maximized;
                this.Topmost = true;
            }
            else
            {
                this.WindowState = m_mainWndState;
                this.WindowStyle = m_mainWndStyle;
                this.Topmost = false;
            }
        }

        private void ExitFullScreen(object sender, ExecutedRoutedEventArgs e)
        {
            m_isFullScreen = false;

            this.WindowState = m_mainWndState;
            this.WindowStyle = m_mainWndStyle;
            this.Topmost = false;
        }

        private void MaximizePreview(object sender, ExecutedRoutedEventArgs e)
        {
            m_isPreviewMaximized = !m_isPreviewMaximized;
            if (m_isPreviewMaximized)
            {
                m_gridColumnDefWidth = gridColumnDefLeftPane.Width;  // Save the current left-pane width so we can restore it
                gridColumnDefLeftPane.Width = new GridLength(0);  // Minimize the treeview pane
            }
            else
            {
                gridColumnDefLeftPane.Width = m_gridColumnDefWidth;  // Restore the previous size
            }
        }

        private void ToggleFileSystem(object sender, ExecutedRoutedEventArgs e)
        {
            m_isFileSystemMaximized = !m_isFileSystemMaximized;

            if (m_isFITSHeaderMaximized)
                RestoreFITSHeader();  // Un-maximize the FITS header pane first

            if (m_isFileSystemMaximized)
            {
                m_gridRowDefHeight = gridRowDefTopPane.Height;  // Save the current top-pane height so we can restore it
                gridRowDefBottomPane.Height = new GridLength(0);  // Minimize the FITS header pane
            }
            else
            {
                gridRowDefBottomPane.Height = m_gridRowDefHeight;  // Restore the previous size
            }
        }

        private void RestoreFileSystem()
        {
            m_isFileSystemMaximized = false;
            gridRowDefBottomPane.Height = m_gridRowDefHeight;  // Restore the previous size
        }

        private void ToggleFITSHeader(object sender, ExecutedRoutedEventArgs e)
        {
            m_isFITSHeaderMaximized = !m_isFITSHeaderMaximized;

            if (m_isFileSystemMaximized)
                RestoreFileSystem();  // Un-maximize the file system pane first

            if (m_isFITSHeaderMaximized)
            {
                m_gridRowDefHeight = gridRowDefBottomPane.Height;  // Save the current bottom-pane height so we can restore it
                gridRowDefTopPane.Height = new GridLength(0);  // Minimize the file system pane
            }
            else
            {
                gridRowDefTopPane.Height = m_gridRowDefHeight;  // Restore the previous size
            }
        }

        private void RestoreFITSHeader()
        {
            m_isFITSHeaderMaximized = false;
            gridRowDefTopPane.Height = m_gridRowDefHeight;  // Restore the previous size
        }
        #endregion

        #region Menu Handlers
        #region File System
        private void Menu_FullScreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullScreen(sender, null);
        }

        private void Menu_FileSystem_TogglePane_Click(object sender, RoutedEventArgs e)
        {
            ToggleFileSystem(sender, null);
        }

        private void Menu_FileSystem_HidePane_Click(object sender, RoutedEventArgs e)
        {
            ToggleFITSHeader(sender, null);
        }
        #endregion

        #region FITS Header
        private void Menu_Header_TogglePane_Click(object sender, RoutedEventArgs e)
        {
            ToggleFITSHeader(sender, null);
        }

        private void Menu_Header_HidePane_Click(object sender, RoutedEventArgs e)
        {
            ToggleFileSystem(sender, null);
        }

        private void Menu_Header_ExportText_Click(object sender, RoutedEventArgs e)
        {
            ExportFITSHeaderData(ExportOptions.ExportAsText);
        }

        private void Menu_Header_ExportCSV_Click(object sender, RoutedEventArgs e)
        {
            ExportFITSHeaderData(ExportOptions.ExportAsCSV);
        }

        private void Menu_Header_ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportFITSHeaderData(ExportOptions.ExportAsExcelSheet);
        }
        #endregion

        #region Thumbnail/Preview
        private void Menu_Preview_TogglePreview_Click(object sender, RoutedEventArgs e)
        {
            MaximizePreview(sender, null);
        }

        private void Menu_Preview_SaveAs_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".jpg";
            dlg.Filter = "Jpeg Image (.jpg)|*.jpg";
            if (!dlg.ShowDialog() == true)
                return;

            string filenamePath = dlg.FileName;

            if (imagePreview == null || imagePreview.Source == null)
                return;

            try
            {
                if (!m_fitsFile.SaveAsJpeg(filenamePath))
                    MessageBox.Show("Unable to save file to " + filenamePath, "File Error");
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FileSystemExplorer.Menu_Preview_SaveAs_Click: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }

        private void Menu_Preview_Rotate90Right_Click(object sender, RoutedEventArgs e)
        {
            RotateImage(90);
        }

        private void Menu_Preview_Rotate90Left_Click(object sender, RoutedEventArgs e)
        {
            RotateImage(-90);
        }
        #endregion
        #endregion
    }
}
