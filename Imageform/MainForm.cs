using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Imageform
{
    public partial class MainForm : Form
    {
        // Variables
        private Bitmap originalImage;

        private int blockSize;
        private int threshold;
        private int rate;

        // Used to update UI after processing thread is done
        public delegate void UpdateUICallback();

        // Constructor
        public MainForm()
        {
            InitializeComponent();

            // Instructions into the first imagebox
            pictureBox1.Image = Properties.Resources.placeholder1;

            // Initialize values from counters
            blockSize = (int)blockCounter.Value;
            threshold = (int)thresholdCounter.Value;
            rate = (int)rateCounter.Value;
            compressButton.Enabled = false;

        }

        // Select-button has been clicked, open a dialog to select an image
        private void selectButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            //dialog.Filter = "JPEG (*.jpeg)|*.jpeg|PNG (*.png)|*.png|
            //                   JPG (*.jpg)|*.jpg|TIFF (*.tif)|*.tif";
            dialog.Filter = "Image files|*.jpeg;*.jpg;*.png;*.tif;*.tiff;*.bmp";
            dialog.InitialDirectory = @"C:\";
            dialog.Title = "Select an image to compress.";

            // If image selected
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                originalImage = new Bitmap(Image.FromFile(dialog.FileName));
                pictureBox1.Image = originalImage;

                // More instructions
                pictureBox2.Image = Properties.Resources.placeholder2;
                compressButton.Enabled = true;
                psnrLabel.Text = "PSNR: undef";
            }
        }

        // Compress-button has been clicked
        private void compressButton_Click(object sender, EventArgs e)
        {
            // Blocksize is larger than image dimensions
            if ( blockSize > originalImage.Width || blockSize > originalImage.Height)
            {
                pictureBox2.Image = Properties.Resources.placeholder4;
                return;
            }

            pictureBox2.Image = Properties.Resources.placeholder5;
            
           	// Start a new thread for the image compression
            new Thread(new ThreadStart(() => {
                ImageProcessor.compressImage(
                    originalImage,
                    blockSize,
                    threshold,
                    rate);
            })).Start();

            // Disable buttons until the image is processed
            selectButton.Enabled = false;
            compressButton.Enabled = false;
            blockCounter.Enabled = false;
            thresholdCounter.Enabled = false;
            rateCounter.Enabled = false;

            // Link the event from the DCT-class into handler in here
            ImageProcessor.OnImageProcessed += DCT_OnImageProcessed;
        }

        // Event handler, responds to event from DCT-class
        void DCT_OnImageProcessed(Bitmap ProcessedImage)
        {
            pictureBox2.Image = ProcessedImage;

            // Invoke a callbackfunction to update UI-elements
            psnrLabel.BeginInvoke(new UpdateUICallback(this.updateUI));
        }

        // Updates the UI-elements after compression is done
        private void updateUI()
        {
            // Wait for the other thread to set the picturebox2
            Thread.Sleep(100);

            // Calculate MSE for the images
            Bitmap img1 = originalImage;
            Bitmap img2 = new Bitmap(pictureBox2.Image);

            double mse = 0;
            int width = img1.Width;
            int height = img1.Height;
            

            for (int ii = 0; ii < width; ii++)
            {
                for (int jj = 0; jj < height; jj++)
                {
                    mse += Math.Pow(((img1.GetPixel(ii, jj).R +
                        img1.GetPixel(ii, jj).G +
                        img1.GetPixel(ii, jj).B) / 3) -

                        ((img2.GetPixel(ii, jj).R +
                        img2.GetPixel(ii, jj).G +
                        img2.GetPixel(ii, jj).B) / 3),
                        2);
                }
            }
            mse = mse / (width * height);

            // Calculate and set the PSNR
            double psnr = 10 * Math.Log10(Math.Pow(255, 2) / mse);
            psnrLabel.Text = string.Format("PSNR: {0:0.##}", psnr);

            // Enable controls again
            compressButton.Enabled = true;
            selectButton.Enabled = true;
            blockCounter.Enabled = true;
            thresholdCounter.Enabled = true;
            rateCounter.Enabled = true;
        }

        private void blockCounter_ValueChanged(object sender, EventArgs e)
        {
            int val = (int)blockCounter.Value;

            // Check if the current value is already a power of 2
            // (handler has been triggered by assignment in the end)
            if ((val & (val-1)) == 0)
                return;

            if (val > blockSize){
                blockSize *= 2;
            } else {
                blockSize /= 2;
            }
            
            blockCounter.Value = blockSize;
        }

        private void thresholdCounter_ValueChanged(object sender, EventArgs e)
        {
            threshold = (int)thresholdCounter.Value;
        }

        private void rateCounter_ValueChanged(object sender, EventArgs e)
        {
            rate = (int)rateCounter.Value;
        }
    }
}
