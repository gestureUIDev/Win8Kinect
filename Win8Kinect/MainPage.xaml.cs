using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using WindowsPreview.Kinect;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Win8Kinect
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        KinectSensor _kinect;
        InfraredFrameReader _IFReader;
        private ushort[] _infraredFrameData;
        private byte[] _infraredPixels;
        const int _bytesPerPixel = 4;
        private WriteableBitmap _bitmap;

        /// <summary>
        /// The highest value that can be returned in the InfraredFrame.
        /// It is cast to a float for readability in the visualization code.
        /// </summary>
        private const float InfraredSourceValueMaximum =
            (float)ushort.MaxValue;

        /// </summary>
        /// Used to set the lower limit, post processing, of the
        /// infrared data that we will render.
        /// Increasing or decreasing this value sets a brightness
        /// "wall" either closer or further away.
        /// </summary>
        private const float InfraredOutputValueMinimum = 0.01f;

        /// <summary>
        /// The upper limit, post processing, of the
        /// infrared data that will render.
        /// </summary>
        private const float InfraredOutputValueMaximum = 1.0f;

        /// <summary>
        /// The InfraredSceneValueAverage value specifies the 
        /// average infrared value of the scene. 
        /// This value was selected by analyzing the average
        /// pixel intensity for a given scene.
        /// This could be calculated at runtime to handle different
        /// IR conditions of a scene (outside vs inside).
        /// </summary>
        private const float InfraredSceneValueAverage = 0.08f;

        /// <summary>
        /// The InfraredSceneStandardDeviations value specifies 
        /// the number of standard deviations to apply to
        /// InfraredSceneValueAverage.
        /// This value was selected by analyzing data from a given scene.
        /// This could be calculated at runtime to handle different
        /// IR conditions of a scene (outside vs inside).
        /// </summary>
        private const float InfraredSceneStandardDeviations = 3.0f;




        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            this.Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if ((_kinect != null) && (_kinect.IsOpen))
            {
                _kinect.Close();
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            this._kinect = KinectSensor.GetDefault();
            if (_kinect == null) return;


            this._kinect.Open();
            if (this._kinect.IsOpen)
            {
                tblStatus.Text = "Kinect Opened successfully";
            }
            // start getting frames here.
            getInfraRedFrames();
        }

        private void getInfraRedFrames()
        {
            FrameDescription infraredFrameDesc = this._kinect.InfraredFrameSource.FrameDescription;
            // open the reader
            this._IFReader = 
                this._kinect.InfraredFrameSource.OpenReader();

            // frames arrival hander.
            this._IFReader.FrameArrived += _IFReader_FrameArrived;

            // allocate the space for pixels and buffers
            this._infraredFrameData = new ushort[infraredFrameDesc.Height * infraredFrameDesc.Width];
            this._infraredPixels = new byte[infraredFrameDesc.Height * infraredFrameDesc.Width * _bytesPerPixel];

            this._bitmap = new WriteableBitmap(infraredFrameDesc.Width, infraredFrameDesc.Height);


        }

        private void _IFReader_FrameArrived(InfraredFrameReader sender, 
            InfraredFrameArrivedEventArgs args)
        {
            // do the work with the frames.
            // process the frame
            bool infraredFrameProcessed = false;

            using (InfraredFrame infraredFrame = 
                args.FrameReference.AcquireFrame() )
            {
                if(infraredFrame != null)
                {
                    FrameDescription infraredFrameDescription =
                                    infraredFrame.FrameDescription;

                    // verify the frame data and write to the display bitmap
                    if (((infraredFrameDescription.Width *
                                infraredFrameDescription.Height)
                                == this._infraredFrameData.Length) &&
                        (infraredFrameDescription.Width ==
                            this._bitmap.PixelWidth) &&
                        (infraredFrameDescription.Height ==
                            this._bitmap.PixelHeight))
                    {
                        // Copy the pixel data from the image to a 
                        // temporary array
                        infraredFrame.CopyFrameDataToArray(
                            this._infraredFrameData);

                        infraredFrameProcessed = true;
                    }
                } // end if(infraredFrame != null)
            } // endusing

            if( infraredFrameProcessed == true)
            {
                // render to the screen.
                convertFrameDataToPixels();
                renderPixelArray(this._infraredPixels);
            }
        }

        private void renderPixelArray(byte[] infraredPixels)
        {
            infraredPixels.CopyTo(this._bitmap.PixelBuffer);
            this._bitmap.Invalidate();
            FrameDisplayImage.Source = this._bitmap;
        }

        private void convertFrameDataToPixels()
        {
            // Convert the infrared to RGB
            int colorPixelIndex = 0;
            for (int i = 0; i < this._infraredFrameData.Length; ++i)
            {
                // normalize the incoming infrared data (ushort) to 
                // a float ranging from InfraredOutputValueMinimum
                // to InfraredOutputValueMaximum] by

                // 1. dividing the incoming value by the 
                // source maximum value
                float intensityRatio = (float)this._infraredFrameData[i] 
                                        /
                                    InfraredSourceValueMaximum;

                // 2. dividing by the 
                // (average scene value * standard deviations)
                intensityRatio /=
                 InfraredSceneValueAverage * InfraredSceneStandardDeviations;

                // 3. limiting the value to InfraredOutputValueMaximum
                intensityRatio = Math.Min(InfraredOutputValueMaximum,
                    intensityRatio);

                // 4. limiting the lower value InfraredOutputValueMinimum
                intensityRatio = Math.Max(InfraredOutputValueMinimum,
                    intensityRatio);

                // 5. converting the normalized value to a byte and using 
                // the result as the RGB components required by the image
                byte intensity = (byte)(intensityRatio * 255.0f);
                this._infraredPixels[colorPixelIndex++] = intensity; //Blue
                this._infraredPixels[colorPixelIndex++] = intensity; //Green
                this._infraredPixels[colorPixelIndex++] = intensity; //Red
                this._infraredPixels[colorPixelIndex++] = 255;       //Alpha           
            }

        }
    }
}
