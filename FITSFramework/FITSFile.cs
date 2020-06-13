#region Using statements
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;  // Requires reference to PresentationCore.dll
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
#endregion

namespace FITSFramework
{
    public class FITSFile
    {
        #region Constant values and Enums
        private const int       DATA_BLOCK_SIZE                     = 2880;     // The size of all FITS data blocks (header and image)
        private const double    IMAGE_DPI                           = 96;       // The DPI to use when preparing a bitmap for display
        private const int       BITSPERPIXEL_BYTE                   = 8;        // Number of bytes in a 8-bit pixel
        private const int       BITSPERPIXEL_USHORT                 = 16;       // Number of bytes in a 16-bit signed pixel
        private const int       BITSPERPIXEL_UINT                   = 32;       // Number of bytes in a 32-bit signed pixel
        private const int       BITSPERPIXEL_FLOAT                  = 32;       // Number of bytes in a 32-bit floating point pixel

        public enum             ImageStreamTo { TemporaryImageFile, InMemoryImage };
        #endregion

        #region Private Member variables
        private List<FITSHeaderItem> m_fitsHeaderItems; // List of FITSHeaderItems that describe the properties of the encapsulated FITS file (for display-only)
        private List<FITSHeaderItem> m_keyHeaderItems;  // List of important header items that are parsed to concrete numeric values (BITPIX, etc.)
        private BitmapSource m_imageSource;             // A JPEG image source, suitable for display using a WPF Image control, etc.
        private Type m_pixelDataType;                   // Holds the true data type being used to hold image data

        private string m_fileName;                      // Filename (including path) of the FITS file encapsulated by this class
        private bool m_fileExists;                      // Flags if the encapsulated file exists
        private bool m_isValidFitsFile;                 // Flags if we're encapsulating a valid FITS file (i.e. the header has been read)  
        private bool m_isRGBImage;                      // Flags if the file contains three data sets (one for each R, G and B image)     
        private bool m_successfulDataRead;              // True if we've successfully read and displayed a preview of the FITS image data
        private byte[] m_byteImageBuffer;               // Holds the array of scaled/offset pixel data in byte format
        private byte[] m_byteImageBufferSave;           // Holds a copy of the array of pixel data in byte format before it's scaled/offset/strecthed
        private short[] m_shortImageBuffer;             // Holds the array of scaled/offset pixel data in ushort format        
        private short[] m_shortImageBufferSave;         // Holds a copy of the array of pixel data in ushort format before it's scaled/offset/stretched
        private int[] m_intImageBuffer;                 // Holds the array of scaled/offset pixel data in int format
        private int[] m_intImageBufferSave;             // Holds a copy of the array of scaled/offset pixel data in int format before it's scaled/offset/stretched
        private float[] m_floatImageBuffer;             // Holds the array of scaled/offset pixel data in float format
        private float[] m_floatImageBufferSave;         // Holds a copy of the array of scaled/offset pixel data in float format
        private double[] m_doubleImageBuffer;           // Holds the array of scaled/offset pixel data in double format
        private double[] m_doubleImageBufferSave;       // Holds a copy of the array of scaled/offset pixel data in double format
        private double m_pixelDataMin;                  // The minimum value the pixel data type can represent
        private double m_pixelDataMax;                  // The maximum value the pixel data type can represent
        private long m_eoHeader;                        // The position of end of the header/start of image data
        private ushort[] m_ushortImageBuffer;           // Holds the array of scaled/offset pixel data in ushort format - this is what's used to construct the bitmap ready for encoding as a jpeg
        private int m_stride;                           // The stride value used when encoding the image
        private int m_pixelDataSize;                    // The size of the buffer that holds the raw pixel data
        #endregion

        #region Properties
        public string FileName                          { get { return m_fileName; } set { m_fileName = value; m_fileExists = File.Exists(m_fileName); }}  // The path/filename of the FITS file we're encapsulating
        public bool IsValidFITSFile                     { get { return m_isValidFitsFile; }}                                // Flags if we're encapsulating a valid FITS file (i.e. the header has been read) 
        public bool FITSFileExists                      { get { return m_fileExists; }}                                     // True if the encapsulated file exists, false other
        public List<FITSHeaderItem> FITSHeaderItems     { get { return m_fitsHeaderItems; }}                                // List of all header items in the FITS header
        public BitmapSource ImageSource                 { get { return m_imageSource; }}                                    // An encoded image source suitable for displaying with a WPF Image control, etc.
        public int BitsPerPixel                         { get { return m_keyHeaderItems.GetHeaderIntItem("BITPIX"); }}      // Bit-depth of the image (8 unsigned int, 16 & 32 int, -32 & -64 real)
        public int NumberOfAxes                         { get { return m_keyHeaderItems.GetHeaderIntItem("NAXIS"); }}       // The number of image axes (normally 2 (width/height) for grayscale images, and 3 for RGB)
        public int ImageWidth                           { get { return m_keyHeaderItems.GetHeaderIntItem("NAXIS1"); }}      // Width of the image
        public int ImageHeight                          { get { return m_keyHeaderItems.GetHeaderIntItem("NAXIS2"); }}      // Height of the image
        public int StretchRangeBlack                    { get { return m_keyHeaderItems.GetHeaderIntItem("CBLACK"); }}      // Image stretch (black) minimum point. When the data is "stretched" all pixel values <= this threshold are set to zero (black)
        public int StretchRangeWhite                    { get { return m_keyHeaderItems.GetHeaderIntItem("CWHITE"); }}      // Image stretch (white) maximum point. When the data is "stretched" all pixel values >= this threshold are set to the type's max value (white). Data between the black/white thresholds is proportionatly adjusted
        public bool IsRGBImage                          { get { return m_isRGBImage; }}                                     // True if the NAXIS3 is present and has the value "3", otherwise it will be false
        public float ImageScaleFactor                   { get { return m_keyHeaderItems.GetHeaderFloatItem("BSCALE"); }}    // Image date scale factor. This value is always used to multiply the raw pixel value when reading and converting image data
        public float ImageZeroOffsetFactor              { get { return m_keyHeaderItems.GetHeaderFloatItem("BZERO"); }}     // Image data zero-offset factor. This value is added to each raw pixel value when reading and converting image data
        public Type PixelDataType                       { get { return m_pixelDataType; }}                                  // The underlying true image pixel data type
        public double PixelDataMin                      { get { return m_pixelDataMin; }}                                   // The minimum value the pixel data type can represent
        public double PixelDataMax                      { get { return m_pixelDataMax; }}                                   // The maximum value the pixel data type can represent
        public int Stride                               { get { return m_stride; }}                                         // The stride value used when encoding the image
        public int PixelDataSize                        { get { return m_pixelDataSize; }}                                  // The size of the buffer that holds the raw pixel data
        #endregion

        #region Construction
        public FITSFile() : this(null) {}
        public FITSFile(string fileName)
        {
            m_successfulDataRead = false;
            m_fileName = fileName;

            if(string.IsNullOrEmpty(fileName))
                m_fileExists = false;
            else
                m_fileExists = File.Exists(m_fileName);

            m_isValidFitsFile = false;  // Set to true when the header is successfully read
            m_fitsHeaderItems = new List<FITSHeaderItem>();

            // Init the keys for the important header items with default values (these will be parsed to actual values when the FITS header text is read)...
            // Property             FITS Header Key     Type    Default Value   Notes
            // ------------------------------------------------------------------------------------------------------
            // BitsPerPixel         BITPIX              Int     16              16 bits per pixel
            // NumberOfAxes         NAXIS               Int     2               Grayscale image
            // ImageWidth           NAXIS1              Int     0               No sensible default
            // ImageHeight          NAXIS2              Int     0               No sensible default
            // IsRGBImage           NAXIS3              Int     0               0 = 'not present', otherwise it will have the value 3.
            // StretchRangeMin      CBLACK              Int     0               The image stretch (black) minimum point 
            // StretchRangeMax      CWHITE              Int     65535           The image stretch (white) maximum point
            // ImageScaleFactor     BSCALE              Float   1               Raw pixel data multiplier
            // ImageZeroFactor      BZERO               Float   0               Raw pixel data addition

            m_keyHeaderItems = new List<FITSHeaderItem>();
            m_keyHeaderItems.Add(new FITSHeaderItem("BITPIX", "16",    "", typeof(int)));
            m_keyHeaderItems.Add(new FITSHeaderItem("NAXIS",  "2",     "", typeof(int)));
            m_keyHeaderItems.Add(new FITSHeaderItem("NAXIS1", "0",     "", typeof(int)));
            m_keyHeaderItems.Add(new FITSHeaderItem("NAXIS2", "0",     "", typeof(int)));
            m_keyHeaderItems.Add(new FITSHeaderItem("NAXIS3", "0",     "", typeof(int)));
            m_keyHeaderItems.Add(new FITSHeaderItem("CBLACK", "0",     "", typeof(int)));
            m_keyHeaderItems.Add(new FITSHeaderItem("CWHITE", "65535", "", typeof(int)));
            m_keyHeaderItems.Add(new FITSHeaderItem("BSCALE", "1.0",   "", typeof(float)));
            m_keyHeaderItems.Add(new FITSHeaderItem("BZERO",  "0.0",   "", typeof(float)));
        }
        #endregion

        #region Public Header Methods
        public bool ReadHeader()
        {
            if (!m_fileExists)
                return false;

            BinaryReader reader = null;
            byte[] headerBuffer = null;
            int readSize = 0;
            m_eoHeader = -1;

            try
            {
                headerBuffer = new byte[80];  // The header will always be a multiple of 2880 bytes, with each header item being exactly 80-bytes long
                reader = new BinaryReader(File.Open(m_fileName, FileMode.Open));
                do
                {
                    readSize += reader.Read(headerBuffer, 0, 80);
                } 
                while(ParseHeaderBuffer(headerBuffer));  // Continue reading one 80-char 'line' of the header until we reach the 'END' marker

                // We now need to calculate where the end of the current 2880-byte block (which will be padded with NULLS/ASCII blanks) will be
                // and record the position start of the first 2880-byte data block - this allows us to seek directly to the image (skipping 
                // the header) as required...
                m_eoHeader = (reader.BaseStream.Position + DATA_BLOCK_SIZE) - (reader.BaseStream.Position % DATA_BLOCK_SIZE) +1;

                if (ParseKeyHeaderValues())  // Complete the parsing of the header by parsing the fundamental FITS file header properties (BITPIX, NAXIS, etc.)
                    return true;

                return false;  // Couldn't successfuly parse the header
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.ReadHeader: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
            finally
            {
                reader.Close();
            }

            return false;
        }
        #endregion

        #region Private Header Methods
        private bool ParseHeaderBuffer(byte[] buf)
        {
            System.Text.Encoding encoder = null;
            string headerString = null;

            if (buf == null || buf.Length == 0)
                return false;
            try
            {
                encoder = System.Text.Encoding.ASCII;
                headerString = encoder.GetString(buf);

                if (headerString.StartsWith("END"))  // The end of the header items is denoted by the "END" keyword (with no value or comment) 
                    return false;

                // The header should consist of three parts: keyword = value / comment
                // Keywords are up to 8-chars in length, and may contain chars, 0..9, "-" (hyphen) and "_" (underscore) only
                // Exceptions:
                //   * An empty key with a non-empty comment means this is a continuation of the comment from the previous header item
                //   * Some keywords (like COMMENT and HISTORY) are not followed by "=". In this case, the remainder of the header item contains simple text
                //
                // Therefore, the three possible formats are:
                //
                //   Format #1: "keyword = value / comment"
                //   Format #2: "COMMENT some comment text..."
                //   Format #3: "comment text..."

                headerString = headerString.Trim();  // Trim all the padding away

                FITSHeaderItem fhi = new FITSHeaderItem();
                int index;
                bool commentAddedToPreviousHeaderItem = false;

                // Parse format #1:
                index = headerString.IndexOf('=', 0, 9);
                if (index != -1)  // Is there a "=" somewhere in the first 10-chars?
                {
                    fhi.Key = headerString.Substring(0, index - 1);  // Add the keyword to the FITSHeaderItem

                    // Is this a header item we want to display?
                    if(!FITSHeaderItem.IsDisplayableHeaderItem(fhi.Key))
                        return true;  // Tell the caller to continue reading the header

                    headerString = headerString.Substring(index + 1);  // Chop off the keyword
                    index = headerString.LastIndexOf('/');  // Do we have a comment? It will be preceeded by a '/' (but there can also be '/' chars in date fields, etc. So we need to find the right-most (last) instance of a '/')
                    if (index != -1)
                    {
                        fhi.Comment = headerString.Substring(index + 1);  // Add the comment to the FITSHeaderItem
                        headerString = headerString.Substring(0, index - 1);  // Chop off the comment
                    }

                    fhi.Value = headerString.Trim();  // Add the value (as a string) to the FITSHeaderItem
                }
                // Parse format #2;
                else if (headerString.StartsWith("COMMENT"))
                {
                    fhi.Comment = headerString.Substring("COMMENT".Length);
                }
                // Parse format #3
                else
                {
                    if (m_fitsHeaderItems.Count > 0)
                    {
                        commentAddedToPreviousHeaderItem = true;
                        m_fitsHeaderItems[m_fitsHeaderItems.Count - 1].Comment = headerString;  // Get the previous header item
                    }
                    else
                        fhi.Comment = headerString;
                }

                if(!commentAddedToPreviousHeaderItem)
                    FITSHeaderItems.Add(fhi);
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.ParseHeaderBuffer: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);

                // Ignore any errors parsing individual FITS header items
                // Additional checks for vital header items will happen at a later stage in processing
                return true;  // Tell the caller to continue reading the header
            }

            return true;  // Return false only if we've come to the end of the header
        }

        private bool ParseKeyHeaderValues()
        {
            foreach (FITSHeaderItem khi in m_keyHeaderItems)
            {
                if (!ParseHeaderValue(khi.Key))
                    return false;
            }

            if (ImageHeight > 0 && ImageWidth > 0)
                m_isValidFitsFile = true;

            return true;
        }

        private bool ParseHeaderValue(string key)
        {
            int itemIntValue;
            float itemFloatValue;

            switch (key)
            {
                case "CWHITE":
                case "CBLACK":
                    itemIntValue = m_fitsHeaderItems.GetHeaderIntItem(key);  // Get the value as a string from the display-only list of header items...
                    if (itemIntValue != -1)
                        m_keyHeaderItems.SetHeaderItem(key, itemIntValue);  // ... and update the default value in the list of key header items

                    return true;  // Always return success as these two values (used to perform a linear 'stretch' on the image) are not vital
                case "BITPIX":
                case "NAXIS":
                case "NAXIS1":
                case "NAXIS2":
                    itemIntValue = m_fitsHeaderItems.GetHeaderIntItem(key);  // Get the value as a string from the display-only list of header items...
                    if (itemIntValue != -1)
                    {
                        m_keyHeaderItems.SetHeaderItem(key, itemIntValue);  // ... and update the default value in the list of key header items
                        return true;
                    }
                    else
                        return false;  // The item is not in the header - we can't decide how to process the image without this info
                case "NAXIS3":
                    // Handled differently from the other key header values - it will only be present if the fits file is an RGB image.
                    // If present, we check its value is 3 (for 3 image planes (R,G,B)). If not, we flag an error. 
                    // Either way, the m_isRGBImage flag is set to show if we're dealing with an RGB image or not
                    itemIntValue = m_fitsHeaderItems.GetHeaderIntItem(key);  // Get the value as a string from the display-only list of header items...
                    if (itemIntValue != -1)
                    {
                        if (itemIntValue != 3)
                            return false;  // Flag an error - the header item should either not be present or set to "3"

                        m_isRGBImage = true;
                    }
                    else
                    {
                        // NAXIS3 is not present in the header - this is a mono image
                        m_isRGBImage = false;
                    }
                    return true;
                case "BSCALE":
                case "BZERO":
                    itemFloatValue = m_fitsHeaderItems.GetHeaderFloatItem(key);  // Get the value as a string from the display-only list of header items...
                    if (itemFloatValue != -1)
                        m_keyHeaderItems.SetHeaderItem(key, itemFloatValue);  // ... and update the default value in the list of key header items

                    return true;  // The item is not in the header - use our defaut value 
            }
            return false;
        }
        #endregion

        #region Public Image Methods
        public Image CreateThumbnail(string fullFilenamePath)
        {
            if (string.IsNullOrEmpty(fullFilenamePath))
                return null;

            Image image = null;
            FileName = fullFilenamePath;

            try
            {
                if (ReadHeader())
                {
                    if (ReadImage(ImageStreamTo.InMemoryImage))
                    {
                        image = new Image();
                        image.Source = ImageSource;
                    }
                }
            }
            catch(Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.CreateThumbnail: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                image = null;
            }

            return image;
        }

        public bool ReadImage(ImageStreamTo imageStreamOption)
        {
            #region Documentation
            // We need to be able to cope with reading a variety of different formats as summarized in the table below...
            //                     Raw FITS data                                           Single plane mono
            // Bits/pixel  BITPIX  Signed/Unsigned  Numeric Type  C# Type  .NET Type       Mono / Color       / Color as RGB
            // ---------------------------------------------------------------------------------------------------------------------------
            // 8           8       Unsigned         Integer       byte     System.Byte     [Y]  / N/A         / N/A
            // 16          16      Signed           Integer       ushort   System.UInt16   [Y]  / [Y]         / [N]
            // 32          32      Signed           Integer       uint     System.UInt32   [Y]  / [Y]         / [N]  
            // 32          -32     N/A              Real          float    System.Single   [N]  / [N]         / [N]
            // 64          -64     N/A              Real          double   System.Double   [N]  / [N]         / [N]  
            //
            // We decide which format and read options are applicable through the key FITS header items...
            //
            // Property             FITS Header Key     Type    Default Value   Notes
            // ------------------------------------------------------------------------------------------------------
            // BitsPerPixel         BITPIX              Int     16              16 bits per pixel
            // NumberOfAxes         NAXIS               Int     2               Grayscale image
            // ImageWidth           NAXIS1              Int     0               No sensible default
            // ImageHeight          NAXIS2              Int     0               No sensible default
            // IsRGBImage           NAXIS3              Int     0               NAXIS3 will not be present for mono images, otherwise it will have the value 3. 
            #endregion

            #region Init
            m_successfulDataRead = false;
            int logicalImageSize = ImageWidth * ImageHeight;
            byte[] rawImageData = null;
            BinaryReader reader = null;
            BitmapSource imageJpeg = null;

            if (!m_fileExists)
                return false;

            if (!m_isValidFitsFile)
            {
                // Try reading (possibly re-reading) the header to see if this is a valid FTS file
                if (!ReadHeader() || m_eoHeader == -1)
                    return false;
            }
            #endregion

            try
            {
                reader = new BinaryReader(File.Open(m_fileName, FileMode.Open));  // Open the FITS file
                if (reader.BaseStream.CanSeek)
                    reader.BaseStream.Position = m_eoHeader;  // Seek to the start of the image data
                else
                    throw new System.NotSupportedException("Unable to seek to end of header data");

                // Process the pixel data according to its type requirements...
                switch (BitsPerPixel)
                {
                    #region 8-bits per pixel processing (byte)
                    case 8:
                        m_pixelDataType         = typeof(byte);  // 0...255
                        m_pixelDataMin          = byte.MinValue;
                        m_pixelDataMax          = byte.MaxValue;
                        rawImageData            = new byte[logicalImageSize]; // Allocate space for the raw image pixels 
                        m_byteImageBuffer       = new byte[logicalImageSize]; // Alloc the image buffer of the correct type for our bits-per-pixel
                        m_byteImageBufferSave   = new byte[logicalImageSize]; // Alloc a saved copy of the image buffer so we can re-stretch as required

                        // Read the raw FITS image data and copy it into an array of the correct type for the bits-per-pixel
                        if (!ReadRawFITSImage(reader, rawImageData, m_byteImageBuffer, logicalImageSize, 1))
                            return false;

                        // Make a copy of the unscaled/offset/stretched data (so we can re-stretch it in future)
                        Buffer.BlockCopy(m_byteImageBuffer, 0, m_byteImageBufferSave, 0, logicalImageSize);

                        // Do an offset, scale and linear stretch using the offset, scale and black/white points in the FITS header...
                        FITSUtil.DoScaleBytePixelData(m_byteImageBuffer, logicalImageSize, ImageZeroOffsetFactor,
                            ImageScaleFactor, (byte)StretchRangeBlack, (byte)StretchRangeWhite);

                        // Create a bitmap image source from the pixel data
                        imageJpeg = CreateBitmapSource(PixelFormats.Gray8, m_byteImageBuffer, BitsPerPixel);
                        if (imageJpeg == null)
                            return false;

                        // Save the image (normally as a memory stream, as it's faster) and then encode it as a jpeg ready for display
                        if (!StreamAndEncodeImage(imageJpeg, imageStreamOption, m_fileName))
                            return false;

                        m_successfulDataRead = true;
                        break;
                    #endregion

                    #region 16-bits per pixel processing (16-bit signed short int -> 16-bit unsigned short int)
                    case 16:
                        m_pixelDataType         = typeof(ushort);  // 0...65,535
                        m_pixelDataMin          = ushort.MinValue;
                        m_pixelDataMax          = ushort.MaxValue;
                        rawImageData            = new byte[2 * logicalImageSize];  // Allocate space for the raw image pixels - each two-byte pair represents a single 16-bit ushort pixel value (therefore 2 * (width * height))
                        m_shortImageBuffer      = new short[logicalImageSize];     // FITS image data in its native format
                        m_shortImageBufferSave  = new short[logicalImageSize];     // A copy of the FITS image data in its native format (so we can allow the user to stretch the data)
                        m_ushortImageBuffer     = new ushort[logicalImageSize];    // Alloc the final image buffer

                        // Read the raw FITS image data and copy it into an array of the correct type for the bits-per-pixel
                        if (!ReadRawFITSImage(reader, rawImageData, m_shortImageBuffer, logicalImageSize, 2))
                            return false;

                        // Make a copy of the unscaled/offset/stretched data (so we can re-stretch it in future)
                        Buffer.BlockCopy(m_shortImageBuffer, 0, m_shortImageBufferSave, 0, 2 * logicalImageSize);

                        // Do an offset, scale and linear stretch using the offset, scale and black/white points in the FITS header...
                        FITSUtil.DoScaleShortIntPixelDataToUShort(
                            m_shortImageBuffer,     // The raw signed short int pixel data from the FITS file
                            m_ushortImageBuffer,    // The scaled, offset, stretched and converted unsigned short int pixel data
                            logicalImageSize,       // Size of the buffer
                            ImageZeroOffsetFactor,  // The FITS header value used to convert signed values to unsigned (BZERO)
                            ImageScaleFactor,       // The FITS header value used to convert signed values to unsigned (BSCALE)
                            StretchRangeBlack,      // The value threshold below which pixel data is set to zero (black)
                            StretchRangeWhite);     // The value threshold above which pixel data is set to white

                        // Create a bitmap image source from the pixel data
                        imageJpeg = CreateBitmapSource(PixelFormats.Gray16, m_ushortImageBuffer, BitsPerPixel);  
                        if (imageJpeg == null)
                            return false;
                        
                        // Save the image (normally as a memory stream, as it's faster) and then encode it as a jpeg ready for display
                        if (!StreamAndEncodeImage(imageJpeg, imageStreamOption, m_fileName))
                            return false;

                        m_successfulDataRead = true;
                        break;
                    #endregion

                    #region 32-bits per pixel processing (32-bit signed int -> 16-bit unsigned short int)
                    case 32:
                    case -32:
                        // Note: When reading 32-bit floating point format FITS files we actually need to read the data as if
                        // it were in 32-bit signed integer format

                        m_pixelDataType         = typeof(uint);
                        m_pixelDataMin          = uint.MinValue;
                        m_pixelDataMax          = uint.MaxValue;
                        rawImageData            = new byte[4 * logicalImageSize];  // Allocate space for the raw image pixels (4-bytes per pixel)
                        m_intImageBuffer        = new int[logicalImageSize];       // FITS image data in its native format
                        m_intImageBufferSave    = new int[logicalImageSize];       // FITS image data in its native format (saved for later re-stretch)
                        m_ushortImageBuffer     = new ushort[logicalImageSize];    // Alloc the final image buffer

                        // Read the raw FITS image data and copy it into an array of the correct type for the bits-per-pixel
                        if (!ReadRawFITSImage(reader, rawImageData, m_intImageBuffer, logicalImageSize, 4))
                            return false;

                        // Make a copy of the unscaled/offset/stretched data (so we can re-stretch it in future)
                        Buffer.BlockCopy(m_intImageBuffer, 0, m_intImageBufferSave, 0, 4 * logicalImageSize);

                        //if (BitsPerPixel == -32)
                        //{
                        //    m_keyHeaderItems.SetHeaderItem("CBLACK", ushort.MinValue);
                        //    m_keyHeaderItems.SetHeaderItem("CWHITE", ushort.MaxValue);
                        //}

                        FITSUtil.DoScaleIntPixelDataToUShort(
                            m_intImageBuffer,
                            m_ushortImageBuffer,
                            logicalImageSize,
                            StretchRangeBlack,
                            StretchRangeWhite,
                            BitsPerPixel);

                        // Create a bitmap image source from the pixel data
                        imageJpeg = CreateBitmapSource(PixelFormats.Gray16, m_ushortImageBuffer, BITSPERPIXEL_USHORT);
                        if (imageJpeg == null)
                            return false;

                        // Save the image (normally as a memory stream, as it's faster) and then encode it as a jpeg ready for display
                        if (!StreamAndEncodeImage(imageJpeg, imageStreamOption, m_fileName))
                            return false;

                        m_successfulDataRead = true;
                        break;
                    #endregion

                    #region Unsupported format processing
                    default:
                        return false;  // Unsupported BitsPerPixel format
                    #endregion
                }

                // Set our ImageSource property (which can be used to display the jpeg preview to the user)
                m_imageSource = imageJpeg;  
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.ReadImage: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
            finally
            {
                reader.Close();
            }

            return true;
        }

        public bool StretchImage(ImageStreamTo imageStreamOption, int newStretchRangeBlack, int newStretchRangeWhite)
        {
            if (!m_successfulDataRead)
                return false;

            int logicalImageSize = ImageWidth * ImageHeight;
            BitmapSource imageJpeg = null;

            try
            {
                m_keyHeaderItems.SetHeaderItem("CBLACK", newStretchRangeBlack);
                m_keyHeaderItems.SetHeaderItem("CWHITE", newStretchRangeWhite);

                switch (BitsPerPixel)
                {
                    #region 8-bits per pixel processing (byte)
                    case 8:
                        if (newStretchRangeBlack < byte.MinValue || 
                            newStretchRangeBlack > byte.MaxValue || 
                            newStretchRangeWhite < byte.MinValue || 
                            newStretchRangeWhite > byte.MaxValue)
                            return false;

                        // Get our saved original unscaled/offset/stretched data so we can restretch it
                        Buffer.BlockCopy(m_byteImageBufferSave, 0, m_byteImageBuffer, 0, logicalImageSize);

                        // Scale/offset/stretch the data
                        FITSUtil.DoScaleBytePixelData(
                            m_byteImageBuffer, 
                            logicalImageSize, 
                            ImageZeroOffsetFactor, 
                            ImageScaleFactor, 
                            (byte)StretchRangeBlack, 
                            (byte)StretchRangeWhite);

                        imageJpeg = CreateBitmapSource(PixelFormats.Gray8, m_byteImageBuffer, BitsPerPixel);  // Create a bitmap
                        break;
                    #endregion

                    #region 16-bits per pixel processing (16-bit signed short int -> 16-bit unsigned short int)
                    case 16:
                        if (newStretchRangeBlack < ushort.MinValue || 
                            newStretchRangeBlack > ushort.MaxValue || 
                            newStretchRangeWhite < ushort.MinValue || 
                            newStretchRangeWhite > ushort.MaxValue)
                            return false;

                        // Get our saved original unscaled/offset/stretched data so we can restretch it
                        Buffer.BlockCopy(m_shortImageBufferSave, 0, m_shortImageBuffer, 0, 2 * logicalImageSize);

                        // Scale/offset/stretch the data
                        FITSUtil.DoScaleShortIntPixelDataToUShort(
                            m_shortImageBuffer,     // The raw signed short int pixel data from the FITS file
                            m_ushortImageBuffer,    // The scaled, offset, stretched and converted unsigned short int pixel data
                            logicalImageSize,       // Size of the buffer
                            ImageZeroOffsetFactor,  // The FITS header value used to convert signed values to unsigned (BZERO)
                            ImageScaleFactor,       // The FITS header value used to convert signed values to unsigned (BSCALE)
                            StretchRangeBlack,      // The value threshold below which pixel data is set to zero (black)
                            StretchRangeWhite);     // The value threshold above which pixel data is set to white

                        imageJpeg = CreateBitmapSource(PixelFormats.Gray16, m_ushortImageBuffer, BITSPERPIXEL_USHORT);  // Create a bitmap
                        break;
                    #endregion

                    #region 32-bits per pixel processing (32-bit signed int -> 16-bit unsigned short int)
                    case 32:
                    case -32:
                        if (newStretchRangeBlack < ushort.MinValue || 
                            newStretchRangeBlack > ushort.MaxValue || 
                            newStretchRangeWhite < ushort.MinValue || 
                            newStretchRangeWhite > ushort.MaxValue)
                            return false;

                        // Make a copy of the unscaled/offset/stretched byte data (so we can re-stretch it in future)
                        Buffer.BlockCopy(m_intImageBufferSave, 0, m_intImageBuffer, 0, 4 * logicalImageSize);

                        FITSUtil.DoScaleIntPixelDataToUShort(
                            m_intImageBuffer,
                            m_ushortImageBuffer,
                            logicalImageSize,
                            StretchRangeBlack,
                            StretchRangeWhite,
                            BitsPerPixel);

                        imageJpeg = CreateBitmapSource(PixelFormats.Gray16, m_ushortImageBuffer, BITSPERPIXEL_USHORT);  // Create a bitmap
                        break;
                    #endregion

                    #region Unsupported format processing
                    default:
                        return false;  // Unsupported BitsPerPixel format
                    #endregion
                }               

                if (imageJpeg == null)
                    return false;

                if (!StreamAndEncodeImage(imageJpeg, imageStreamOption, m_fileName))  // Encode the bitmap data as a jpeg ready to display
                    return false;

                m_imageSource = imageJpeg;
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.ReadImage: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }

            return true;
        }

        public bool SaveAsJpeg(string file)
        {
            // Note: We can't simply encode the existing image and write it as a stream to a new file. This works, but results
            // in a file that's locked until the app exists. So, we have to save the image to a temporary memory stream, then
            // copy the raw pixels from the image and write it out to a new file...

            Stream streamTmp = null;
            JpegBitmapEncoder imageEncoder = null;

            try
            {
                streamTmp = new MemoryStream(ImageWidth * ImageHeight) as MemoryStream;  // Save the temp jpeg image stream in-memory

                imageEncoder = new JpegBitmapEncoder();
                imageEncoder.Frames.Add(BitmapFrame.Create(this.ImageSource));
                imageEncoder.Save(streamTmp);

                SharedUtil.Util.CopyImageFromStream(streamTmp, file);
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.SaveAsJpeg: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                return false;
            }

            return true;
        }        
        #endregion

        #region Private Image Methods
        private bool ReadRawFITSImage(BinaryReader reader, byte[] rawFitsImage, Array destBuffer, int logicalImageSize, int rawBytesPerPixel)
        {
            try
            {
                // For some weird reason, 32-bit float data aligns differently than all other types, and we need to decrement the
                // current file read position (which will already have been set to the start of the image data)
                if (BitsPerPixel == -32)
                    reader.BaseStream.Position = m_eoHeader - 1;  

                // Read the entire image byte array in one chunk for performance
                reader.Read(rawFitsImage, 0, logicalImageSize * rawBytesPerPixel);

                // Copy the raw (byte) data into the required bits-per-pixel format (MUCH faster than iterating through the raw data)
                Buffer.BlockCopy(rawFitsImage, 0, destBuffer, 0, rawBytesPerPixel * logicalImageSize);

                return true;
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.ReadRawFITSImage: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
            return false;
        }

        private BitmapSource CreateBitmapSource(PixelFormat pixelFormat, Array pixelData, int bitsPerPixel)
        {
            try
            {
                m_stride = ((ImageWidth * bitsPerPixel + (bitsPerPixel - 1)) & ~(bitsPerPixel - 1)) / 8;
                m_pixelDataSize = pixelData.Length;

                return BitmapSource.Create(
                    ImageWidth,     // Image width
                    ImageHeight,    // Image height
                    IMAGE_DPI,      // DPI (x) - looks nasty below 96 dpi
                    IMAGE_DPI,      // DPI (y)
                    pixelFormat,    // Pixel format. Defaults to 65,536 grey shades for 16-bit unsigned CCDs
                    null,           // Not required
                    pixelData,      // The scaled/offset FITS pixel data
                    m_stride);      // The stride calculation (see comment above)
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.CreateBitmapSource: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }

            return null;
        }

        private bool StreamAndEncodeImage(BitmapSource imageJpeg, ImageStreamTo imageStreamOption, string file)
        {
            string tmpFile = null;
            Stream stream = null;
            TiffBitmapEncoder imageEncoder = null;

            try
            {
                // Create a stream to receive the image...
                if (imageStreamOption == ImageStreamTo.InMemoryImage)  // Use a memory stream if possible as it's quicker and avoids potential file locks, etc.
                    stream = new MemoryStream(ImageWidth * ImageHeight) as MemoryStream;  // Save the temp jpeg image stream in-memory
                else
                {
                    tmpFile = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\Temp\FITSExplorerTemp-" + DateTime.Now.Ticks.ToString() + ".jpg";
                    stream = new FileStream(tmpFile, FileMode.Create) as FileStream;  // Save the temp jpeg image stream as a file
                }

                // Encode the bitmap data as an actual jpeg image so we can display it...
                imageEncoder = new TiffBitmapEncoder();
                imageEncoder.Frames.Add(BitmapFrame.Create(imageJpeg));
                imageEncoder.Save(stream);

                return true;
            }
            catch (Exception ex)
            {
                SharedUtil.SharedEventLog.Log("FITSFile.StreamAndEncodeImage: " + ex.Message, System.Diagnostics.EventLogEntryType.Error);
            }
            return false;
        }
        #endregion

        #region Static Methods
        public static bool IsFITSImage(string file)
        {
            string tmp = file.ToLower();
            if(tmp.EndsWith(".fts") || tmp.EndsWith(".fit") || tmp.EndsWith(".fits"))
                return true;

            return false;
        }
        #endregion
    }
}
