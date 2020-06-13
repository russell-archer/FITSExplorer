using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace FITSFramework
{
    public static class FITSUtil
    {
        // ----------------------------------------------------------------------------------------------------------------------
        // *** Imports of functions from our C++ high-performance support framework ***
        // ----------------------------------------------------------------------------------------------------------------------

        // ----------------------------------------------------------------------------------------------------------------------
        [DllImport(@"FITSFrameworkWin32.dll", EntryPoint = "ScaleBytePixelData", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ScaleBytePixelData(
            byte[]      imageBuffer,
            int         imageBufferSize,
            float       offset,
            float       scale,
            byte        stretchRangeBlack,
            byte        stretchRangeWhite,
            byte        minValue,
            byte        maxValue,
            ushort      deviceRange);

        // ----------------------------------------------------------------------------------------------------------------------
        [DllImport(@"FITSFrameworkWin32.dll", EntryPoint = "ScaleShortIntPixelDataToUShort", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ScaleShortIntPixelDataToUShort(
            short[]     imageBuffer,
            ushort[]    imageBufferDestination,
            int         imageBufferSize,
            float       offset,
            float       scale,
            int         stretchRangeBlack,
            int         stretchRangeWhite,
            ushort      targetTypeMinValue,
            ushort      targetTypeMaxValue,
            ushort      deviceRange);

        // ----------------------------------------------------------------------------------------------------------------------
        [DllImport(@"FITSFrameworkWin32.dll", EntryPoint = "ScaleIntPixelDataToUShort", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ScaleIntPixelDataToUShort(
            int[]       imageBuffer,
            ushort[]    imageBufferDestination,
            int         imageBufferSize,
            int         stretchRangeBlack,
            int         stretchRangeWhite,
            ushort      targetTypeMinValue,
            ushort      targetTypeMaxValue,
            ushort      deviceRange,
            int         bitsPerPixel);

        // ----------------------------------------------------------------------------------------------------------------------
        // *** Helper methods to PInvoke C++ framework functions ***
        // ----------------------------------------------------------------------------------------------------------------------

        // ----------------------------------------------------------------------------------------------------------------------
        // DoScaleBytePixelData - 8-bit byte
        // ----------------------------------------------------------------------------------------------------------------------
        public static void DoScaleBytePixelData(
            byte[]      imageBuffer,
            int         imageBufferSize,
            float       offset,
            float       scale,
            byte        stretchRangeBlack,
            byte        stretchRangeWhite)
        {
            ScaleBytePixelData(
                        imageBuffer,
                        imageBufferSize,
                        offset,
                        scale,
                        stretchRangeBlack,
                        stretchRangeWhite,
                        byte.MinValue,
                        byte.MaxValue,
                        ushort.MaxValue);
        }

        // ----------------------------------------------------------------------------------------------------------------------
        // DoScaleShortIntPixelDataToUShort - 16-bit signed short int => 16-bit unsigned short int
        // ----------------------------------------------------------------------------------------------------------------------
        public static void DoScaleShortIntPixelDataToUShort(
            short[]     imageBuffer,
            ushort[]    imageBufferDestination,
            int         imageBufferSize,
            float       offset,
            float       scale,
            int         stretchRangeBlack,
            int         stretchRangeWhite)
        {
            ScaleShortIntPixelDataToUShort(
                        imageBuffer,
                        imageBufferDestination,
                        imageBufferSize,
                        offset,
                        scale,
                        stretchRangeBlack,
                        stretchRangeWhite,
                        ushort.MinValue,
                        ushort.MaxValue,
                        ushort.MaxValue);
        }

        // ----------------------------------------------------------------------------------------------------------------------
        // DoScaleIntPixelDataToUShort - 32-bit signed int => 16-bit unsigned short int
        // ----------------------------------------------------------------------------------------------------------------------
        public static void DoScaleIntPixelDataToUShort(
            int[]       imageBuffer,
            ushort[]    imageBufferDestination,
            int         imageBufferSize,
            int         stretchRangeBlack,
            int         stretchRangeWhite,
            int         bitsPerPixel)
        {
            ScaleIntPixelDataToUShort(
                        imageBuffer,
                        imageBufferDestination,
                        imageBufferSize,
                        stretchRangeBlack,
                        stretchRangeWhite,
                        ushort.MinValue,
                        ushort.MaxValue,
                        ushort.MaxValue,
                        bitsPerPixel);
        }
    }
}
