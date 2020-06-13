using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FITSFramework;

namespace FileSystemExplorerUserControl
{
    public class ThumbnailInfo
    {
        private bool m_fileExists;
        private bool m_thumbnailCreated;
        private string m_fullPath;
        private string m_filename;
        private string m_filenameNoExtension;
        private string m_fileExtension;
        private string m_description;
        private Image m_image;

        public bool FileExists              { get { return m_fileExists; } }
        public bool ThumbnailCreated        { get { return m_thumbnailCreated; } set { m_thumbnailCreated = value; } }        
        public string FullPath              { get { return m_fullPath; } set { m_fullPath = value; } }
        public string Filename              { get { return m_filename; } }
        public string FilenameNoExtension   { get { return m_filenameNoExtension; } }
        public string FileExtension         { get { return m_fileExtension; } }
        public string Description           { get { return m_description; } } 
        public Image ThumbnailImage         { get { return m_image; } set { m_image = value; } }

        public ThumbnailInfo() : this(null) { }
        public ThumbnailInfo(string fullpath)
        {
            m_fullPath = fullpath;

            try
            {
                m_thumbnailCreated = false;

                if (string.IsNullOrEmpty(fullpath) || !System.IO.File.Exists(fullpath))
                {
                    m_fileExists = false;
                    m_filename = "";
                    m_filenameNoExtension = "";
                    m_fileExtension = "";
                    m_description = "";
                    m_image = null;
                }
                else
                {
                    m_fileExists = true;
                    m_filename = System.IO.Path.GetFileName(m_fullPath);
                    m_filenameNoExtension = System.IO.Path.GetFileNameWithoutExtension(m_fullPath);
                    m_fileExtension = System.IO.Path.GetExtension(m_fullPath);
                    m_description = m_filename;
                    m_image = null;
                }
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("ThumbnailInfo.ThumbnailInfo: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
        }

    }
}
