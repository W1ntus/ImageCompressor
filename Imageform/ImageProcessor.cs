using System;
using System.Drawing;

namespace Imageform
{
	public static class ImageProcessor
    {
        #region Public interface

        // Event handlers
        public delegate void OnImageProcessedHandler(Bitmap ProcessedImage);
        public static event OnImageProcessedHandler OnImageProcessed;

        // The only public interface for the class
		public static void compressImage(Bitmap img, int blockSize, int threshold, int rate)
		{
			// Initialize alpha-array
			double[] alpha = create_alpha (blockSize);

            // Converts the bitmap into R, G and B components, adds padding
            double[,] R; double[,] G; double[,] B;
            convertToArray(img, out R, out G, out B, blockSize);

            // DCT
            R = dct(R, blockSize, alpha);
            G = dct(G, blockSize, alpha);
            B = dct(B, blockSize, alpha);
            
            // Compression
            compress(ref R, blockSize, threshold, rate);
            compress(ref G, blockSize, threshold, rate);
            compress(ref B, blockSize, threshold, rate);

            // IDCT
			R = idct(R, blockSize, alpha);
            G = idct(G, blockSize, alpha);
            B = idct(B, blockSize, alpha);

            // Convert array to bitmap, remove padding (we know the
            // original size from img.Size) and return the converted
            // bitmap-image through an event
			if (OnImageProcessed != null) OnImageProcessed(convertToImage (R, G, B, img.Size));
		}

        #endregion

        #region DCT & IDCT

        // Discrete cosine transform
        private static double[,] dct(double[,] imgArray, int blockSize, double[] alpha)
        {
			int width = imgArray.GetLength(0);
            int height = imgArray.GetLength(1);

            // Coefficient matrix we're constructing
            double[,] C = new double[width, height];

            // Iterate through the blocks
            for (int x = 0; x < width; x += blockSize)
            {
                for (int y = 0; y < height; y += blockSize)
                {
                    // Now we are inside a block, clip it and iterate through it
                    double[,] f = clipArray(imgArray, blockSize, x, y);

                    for (int u = x; u < blockSize + x; ++u)
                    {
                        for (int v = y; v < blockSize + y; ++v)
                        {
                            // Calculate a sum inside the dct
                            double sum = 0;
                            for (int ii = 0; ii < blockSize; ++ii)
                            {
                                for (int jj = 0; jj < blockSize; ++jj)
                                {
                                    sum += f[ii, jj] * Math.Cos((2 * ii + 1) * (u - x) * Math.PI / (2 * blockSize)) *
                                                     Math.Cos((2 * jj + 1) * (v - y) * Math.PI / (2 * blockSize));
                                }
                            }
                            C[u, v] = alpha[u - x] * alpha[v - y] * sum;
                        }
                    }
                }
            }
            return C;
        }

        // Inverse discrete cosine transform
        private static double[,] idct(double[,] dct, int blockSize, double[] alpha)
        {
            int width = dct.GetLength(0);
            int height = dct.GetLength(1);

            double[,] temp = new double[width, height];

            // Iterate through blocks
            for (int x = 0; x < width; x += blockSize)
            {
                for (int y = 0; y < height; y += blockSize)
                {
                    // We're inside a block, clip it
                    double[,] f = clipArray(dct, blockSize, x, y); // Block itself

                    // Now start iterating the block
                    for (int b_x = x; b_x < x + blockSize; ++b_x)
                    {
                        for (int b_y = y; b_y < y + blockSize; ++b_y)
                        {
                            // And calculate the inner idct-sum
                            double sum = 0;
                            for (int u = 0; u < blockSize; ++u)
                            {
                                for (int v = 0; v < blockSize; ++v)
                                {
                                    sum += alpha[u] * alpha[v] * f[u, v] *
                                        Math.Cos(((2d * (double)(b_x - x) + 1d) * (double)u * Math.PI) / (2d * (double)blockSize)) *
                                        Math.Cos(((2d * (double)(b_y - y) + 1d) * (double)v * Math.PI) / (2d * (double)blockSize));
                                }
                            }
                            temp[b_x, b_y] = sum;
                        }
                    }
                }
            }
            return temp;
        }

        #endregion

        #region Compression

        // Compresses the image 
        private static void compress(ref double[,] array, int blockSize, int threshold, int rate)
        {
            // Iterate throught the blocks
            for (int x = 0; x < array.GetLength(0); x += blockSize)
            {
                for (int y = 0; y < array.GetLength(1); y += blockSize)
                {
                    // Calculate min, max and step values for one block
                    double max = 0;
                    double min = 0;
                    double[,] f = clipArray(array, blockSize, x, y);

                    calcMaxMinValues(f, ref max, ref min);
                    double step = (max - min) / Math.Pow(2, rate);

                    // Start iterating the block
                    for (int ii = x; ii < x + blockSize; ++ii)
                    {
                        for (int jj = y; jj < y + blockSize; ++jj)
                        {
                            // Check if absolute value is smaller than given threshold
                            if (Math.Abs(array[ii, jj]) < threshold)
                            {
                                array[ii, jj] = 0;
                            }

                            // Quantize the value according to the calculated step
                            else
                            {
                                double temp = max;
                                while (temp > Math.Abs(array[ii, jj])) temp -= step;
                                array[ii, jj] = Math.Sign(array[ii, jj]) * temp;
                            }
                        }
                    }
                }
            }
        }

        // Calculates absolute minimum and maximum values for given matrix
        private static void calcMaxMinValues(double[,] array, ref double max, ref double min)
        {
            for (int ii = 0; ii < array.GetLength(0); ++ii)
            {
                for (int jj = 0; jj < array.GetLength(1); ++jj)
                {
                    if (Math.Abs(array[ii, jj]) > max) max = Math.Abs(array[ii, jj]);
                    if (Math.Abs(array[ii, jj]) < min) min = Math.Abs(array[ii, jj]);
                }
            }
        }

        #endregion

        #region Helper-functions

        // Clips a size*size subarray from the given image array
		private static double[,] clipArray(double[,] array, int size, int x, int y)
		{
			double[,] temp = new double[size, size];
			for (int ii = 0; ii < size; ++ii)
			{
				for (int jj = 0; jj < size; ++jj)
				{
					temp [ii, jj] = array [ii + x, jj + y];
				}
			}
			return temp;
		}

        // Creates the alpha-array used in DCT and IDCT
		private static double[] create_alpha(int blockSize)
        {
            double[] alpha = new double[blockSize];
            for (int ii = 0; ii < blockSize; ++ii)
            {
                alpha[ii] = Math.Sqrt(2d / blockSize);
            }
            alpha[0] = 1d / Math.Sqrt((double)blockSize);
            return alpha;
        }

        #endregion

        #region Conversions

        // Converts a bitmap-image into a multidimensional array
        private static void convertToArray(Bitmap img, out double[,] R, out double[,] G, out double[,] B, int blockSize)
        {
            // Default to image size
            int width = img.Width;
            int height = img.Height;

            // Calculate possible padding
            int blockrows = (int)Math.Floor((double)img.Width / (double)blockSize);
            int blockcols = (int)Math.Floor((double)img.Height / (double)blockSize);
            if (blockrows*blockSize != img.Width) { width = (blockrows + 1) * blockSize; }
            if (blockcols*blockSize != img.Height) { height = (blockcols + 1) * blockSize; }

            R = new double[width, height];
            G = new double[width, height];
            B = new double[width, height];

            // Iterate through the image, get pixel data
            for (int x = 0; x < width; ++x)
            {
                for (int y = 0; y < height; ++y)
                {
                    // Check if we need to pad
                    if (x >= img.Width || y >= img.Height)
                    {
                        R[x, y] = 0; G[x, y] = 0; B[x, y] = 0;
                        continue;
                    }

                    // If not, then just get the pixel values
                    R[x, y] = img.GetPixel(x, y).R;
                    G[x, y] = img.GetPixel(x, y).G;
                    B[x, y] = img.GetPixel(x, y).B;
                }
            }
        }

        // Converts a multidimensional array into a bitmap-image
        private static Bitmap convertToImage(double[,] R, double[,] G, double[,] B, Size imsize)
        {
            // Original image size
            int width = imsize.Width;
            int height = imsize.Height;

            Bitmap temp = new Bitmap(width, height);

            // Iterate through the image arrays
            for (int ii = 0; ii < width; ++ii)
            {
                for (int jj = 0; jj < height; ++jj)
                {
                    // Make sure the values are in the range 0-255, since the quantization algorithm
                    // sometimes goes slightly over the limits and I have no time to hunt for the bugs. :l
                    int r = Math.Max(0, Math.Min(255, (int)R[ii, jj]));
                    int g = Math.Max(0, Math.Min(255, (int)G[ii, jj]));
                    int b = Math.Max(0, Math.Min(255, (int)B[ii, jj]));
                    temp.SetPixel(ii, jj, Color.FromArgb(r, g, b));
                }
            }

            // Note! There is no need to remove padding, since we use the original images
            // size to iterate through the image array.

            return temp;
        }

		#endregion
    }
}
