// 
// FITSFramework support functions implemented in C++ for performance
// 
// Copyright Russell Archer, 2012
//

#include "stdafx.h"

const static int	INT_TO_USHORT_OVERFLOW_THRESHOLD	= 16777215;  // When 32-bit ints are >= to 16777215 they will underflow to 0 when casting them to ushorts
const static int	FLOAT_TO_USHORT_OVERFLOW_THRESHOLD	= 16556319;  // 16056319
const static int	FLOAT_TO_USHORT_UNDERFLOW_THRESHOLD	= 0;  // If a raw pixel value is < 0, it'll overflow to 65,535 (white)

// ------------------------------------------------------------------------------------------------
// ScaleBytePixelData - 8-bit byte
// ------------------------------------------------------------------------------------------------
// Perform an image scale, offset and linear contrast stretch on the specified array of pixel data 
// (which is of type C# = byte, C++ = unsigned char).
//
// For 8-bit FITS images, the raw pixel data is stored in a simple byte format. 

extern "C" __declspec(dllexport) void ScaleBytePixelData(
	unsigned char*		imageBuffer,		// Raw pixel data
	int					imageBufferSize,	// The size of the image buffer (i.e. could be a sub-frame of the image)
	float				offset,				// The amount by which data is offset (incremented/decremented). Passing offset = 0 and scale = 1 results in no scale/offset
	float				scale,				// The amount by which data is scaled (multiplied). Passing offset = 0 and scale = 1 results in no scale/offset
	unsigned char		stretchRangeBlack,	// Threshold where any pixel <= to this value is set to minValue (black)
	unsigned char		stretchRangeWhite,	// Threshold where any pixel >= to this value is set to maxValue (white)
	unsigned char		minValue,			// The smallest value this pixel can have (black)
	unsigned char		maxValue,			// The highest value this pixel can have (white)
	unsigned short int	deviceRange)		// The bit-depth of the display device (e.g. 65,535)
{
	bool doScaleOffset = true;
	if(offset == 0.0 && scale == 1.0)
		doScaleOffset = false;

    for (int i = 0; i < imageBufferSize; i++)
    {
		if(doScaleOffset == 1)
			imageBuffer[i] = (unsigned char)(offset + scale * imageBuffer[i]);  // Scale/offset the image data

        if (imageBuffer[i] <= stretchRangeBlack)  // Stretch the data...
            imageBuffer[i] = minValue;
        else if (imageBuffer[i] >= stretchRangeWhite)
            imageBuffer[i] = maxValue;
        else
            imageBuffer[i] = (unsigned char)(((double)(imageBuffer[i] - stretchRangeBlack) / (double)(stretchRangeWhite - stretchRangeBlack)) * (double)deviceRange);
    }
}

// ------------------------------------------------------------------------------------------------
// ScaleShortIntPixelDataToUShort - 16-bit signed short int => 16-bit unsigned short int
// ------------------------------------------------------------------------------------------------
// Perform an image scale, offset and linear contrast stretch on the specified array of pixel data 
// 
// For 16-bit FITS images, the raw pixel data is stored in 16-bit *signed* integer format. 
// This needs to be converted first to the correct *unsigned* integer format by adding the FITS 
// header offset factor (BZERO). The raw value may also be scaled up/down through the use of the
// BSCALE FITS header value. The unsigned integer data is then linearly contrast stretched according
// to the CBLACK and CWHITE FITS header values.
//
// The equation to perform a linear stretch is:
//
//   Ds = ((Du - Bp) / (Wp - Bp)) * Rs
//
// where:
//   
//   Ds = stretched data
//   Du = raw (unstretched) data
//   Bp = the stretch black-point - all unstretched values <= to Bp are set to the minimum the data type supports (e.g. "black")
//   Wp = the stretch white-point - all unstretched values >= to Wp are set to the maximum value the data type supports (e.g. "white")
//   Rs = The range of values supported by the display device (e.g. screen)
//
// The equation is public domain

extern "C" __declspec(dllexport) void ScaleShortIntPixelDataToUShort(
	short int*			imageBuffer,			// Raw pixel data
	unsigned short int* imageBufferDestination,	// Buffer that will have the scaled data copied into it
	int					imageBufferSize,		// The size of the image buffer (i.e. could be a sub-frame of the image)
	float				offset,					// The amount by which data is offset (incremented/decremented). Passing offset = 0 and scale = 1 results in no scale/offset
	float				scale,					// The amount by which data is scaled (multiplied). Passing offset = 0 and scale = 1 results in no scale/offset
	int					stretchRangeBlack,		// Threshold where any pixel <= to this value is set to minValue (black)
	int					stretchRangeWhite,		// Threshold where any pixel >= to this value is set to maxValue (white)
	unsigned short int	targetTypeMinValue,		// The smallest value this pixel can have (black) - it's a ushort because that's the eventual target type
	unsigned short int	targetTypeMaxValue,		// The highest value this pixel can have (white) 
	unsigned short int	deviceRange)			// The bit-depth of the display device (e.g. 65,535)
{
	bool doScaleOffset = true;
	if(offset == 0.0 && scale == 1.0)
		doScaleOffset = false;

    for (int i = 0; i < imageBufferSize; i++)
    {
		if(doScaleOffset)
			imageBuffer[i] = (short int)(offset + scale * imageBuffer[i]);  // Scale/offset the image data

		imageBufferDestination[i] = (unsigned short int)imageBuffer[i];  // Copy the pixel into the destination buffer

		// Stretch the data...
        if (imageBufferDestination[i] <= stretchRangeBlack)  
            imageBufferDestination[i] = targetTypeMinValue;
        else if (imageBufferDestination[i] >= stretchRangeWhite)
            imageBufferDestination[i] = targetTypeMaxValue;
        else
			imageBufferDestination[i] = (unsigned short int)(((double)(imageBufferDestination[i] - stretchRangeBlack) / (double)(stretchRangeWhite - stretchRangeBlack)) * (double)deviceRange);
    }
}

// ------------------------------------------------------------------------------------------------
// ScaleIntPixelDataToUShort - 32-bit signed int => 16-bit unsigned short int
// ------------------------------------------------------------------------------------------------
// Perform an image scale, offset, range transformation (from 32-bit signed int to 16-bit ushort) 
// and linear contrast stretch on the specified array of pixel data.

extern "C" __declspec(dllexport) void ScaleIntPixelDataToUShort(
	int*				imageBuffer,			// Raw pixel data
	unsigned short int* imageBufferDestination,	// Buffer that will have the scaled data copied into it
	int					imageBufferSize,		// The size of the image buffer (i.e. could be a sub-frame of the image)
	int					stretchRangeBlack,		// Threshold where any pixel <= to this value is set to minValue (black)
	int					stretchRangeWhite,		// Threshold where any pixel >= to this value is set to maxValue (white)
	unsigned short int	targetTypeMinValue,		// The smallest value this pixel can have (black) - it's a ushort because that's the eventual target type
	unsigned short int	targetTypeMaxValue,		// The highest value this pixel can have (white) 
	unsigned short int	deviceRange,			// The maximum value the output device can handle
	int					bitsPerPixel)			// Number of bits per pixel (either 32 = int, or -32 = float)
{
	for (int i = 0; i < imageBufferSize; i++)
	{
		if(bitsPerPixel == 32)
		{
			if (imageBuffer[i] > INT_TO_USHORT_OVERFLOW_THRESHOLD)  // This prevents fully-saturated pixels overflowing (to zero/black) when cast to ushorts
				imageBuffer[i] = (int)targetTypeMaxValue;
		}

		imageBufferDestination[i] = (unsigned short int)imageBuffer[i];  // Copy the pixel into the destination buffer

		// Stretch...
		if (imageBufferDestination[i] <= stretchRangeBlack)
			imageBufferDestination[i] = targetTypeMinValue;
		else if (imageBufferDestination[i] >= stretchRangeWhite)
			imageBufferDestination[i] = targetTypeMaxValue;
		else
			imageBufferDestination[i] = (unsigned short int)(((double)(imageBufferDestination[i] - stretchRangeBlack) / (double)(stretchRangeWhite - stretchRangeBlack)) * (double)deviceRange);
	}
}


