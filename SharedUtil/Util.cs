using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;

namespace SharedUtil
{
    public class Util
    {
        public static bool CopyImage(string fullFilenamePathSource, string fullFilenamePathDest)
        {
            Stream stream = null;
            byte[] pixelBuf = null;

            try
            {
                // Read the raw pixels...
                pixelBuf = File.ReadAllBytes(fullFilenamePathSource); 

                // Create a stream to receive the image...
                stream = new FileStream(fullFilenamePathDest, FileMode.Create) as FileStream;  
                stream.Write(pixelBuf, 0, pixelBuf.Length);
                stream.Close();

                return true;
            }
            catch (Exception ex)
            {
                SharedEventLog.Log("Util.CopyImage: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
            return false;
        }

        public static bool CopyImageFromStream(Stream source, string fullFilenamePathDest)
        {
            Stream stream = null;
            byte[] pixelBuf = null;

            try
            {
                // Create a stream to receive the image...
                pixelBuf = new byte[source.Length];
                source.Seek(0, SeekOrigin.Begin);
                source.Read(pixelBuf, 0, pixelBuf.Length);

                // Write the image...
                stream = new FileStream(fullFilenamePathDest, FileMode.Create) as FileStream; 
                stream.Write(pixelBuf, 0, pixelBuf.Length);
                stream.Close();

                return true;
            }
            catch (Exception ex)
            {
                SharedEventLog.Log("Util.CopyImage: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
            return false;
        }
    }
}
