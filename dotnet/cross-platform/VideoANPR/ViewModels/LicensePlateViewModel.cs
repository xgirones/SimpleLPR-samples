/* 
VideoANPR - Automatic Number Plate Recognition for Video Streams

VideoANPR is a sample C# application that showcases the capabilities of the SimpleLPR ANPR library for processing video streams.
It demonstrates how to leverage computer vision techniques to detect and extract license plate information in real-time.

Author: Xavier Gironés (xavier.girones@warelogic.com)

Features:
- ANPR Processing: VideoANPR utilizes the SimpleLPR ANPR library to perform automatic number plate recognition on video streams.
- Video Capture: The application uses Emgu.CV as a third-party library for video capture, providing a simple and convenient way
  to process video frames. However, it can be easily replaced with any other compatible library if desired.
- Multi-platform User Interface: VideoANPR utilizes Avalonia and ReactiveUI to provide a cross-platform user interface,
  enabling the application to run on both Windows and Linux systems seamlessly.

Requirements:
- .NET Core SDK 6.0 or higher
- SimpleLPR ANPR library
- Emgu.CV (or alternative third-party library for video capture)
- Avalonia and ReactiveUI

Contributions and feedback are welcome! If you encounter any issues, have suggestions for improvements, or want to add new features,
please submit a pull request or open an issue on the GitHub repository.

Disclaimer: VideoANPR is intended for educational and research purposes only.
*/

using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Structure;
using Avalonia;
using Avalonia.Platform;
using VideoANPR.Observables;
using SimpleLPR3;

using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace VideoANPR.ViewModels
{
    public class LicensePlateViewModel : ViewModelBase
    {
        // Constants defining the maximum image width and height.
        private const int MaxImageWidth = 150;
        private const int MaxImageHeight = 100;
        private const float MaxImageCoordH = (float)(MaxImageWidth - 1);
        private const float MaxImageCoordV = (float)(MaxImageHeight - 1);

        // License plate information.
        public string Text { get; }
        public string CountryCode { get; }
        public TimeSpan TimeStamp { get; }

        // License plate image.
        public Bitmap Image { get; private set; }

        // Summary of the license plate information.
        public string Summary { get => string.IsNullOrWhiteSpace(CountryCode) ?
                                            string.Format("{0:g}    {1}", TimeStamp, Text) :
                                            string.Format("{0:g}    [{1}] {2}", TimeStamp, CountryCode, Text); }

        // Method for cropping the license plate from the video frame.
        private Bitmap CropLicensePlate(Emgu.CV.Mat frame, Candidate lp)
        {
            // Get the vertices of the license plate region.
            Vector2[] lpv = Util.GetPlateRegionVertices(lp).Select(x => new Vector2(x.X, x.Y)).ToArray();

            // Define the source and destination vertices for perspective transformation.
            float fExtentH = Vector2.Distance(lpv[0], lpv[1]);
            float fExtentV = Vector2.Distance(lpv[0], lpv[3]);
            float fScale =
                fExtentV * MaxImageCoordH <= fExtentH * MaxImageCoordV ?
                    MaxImageCoordH / (fExtentH + float.Epsilon) :
                    MaxImageCoordV / (fExtentV + float.Epsilon);

            PointF[] srcVert = lpv.Select(x => new PointF(x.X, x.Y)).ToArray();
            PointF[] dstVert = new PointF[]
            {
                new PointF(0f, 0f),
                new PointF(fExtentH * fScale, 0f),
                new PointF(fExtentH * fScale, fExtentV * fScale),
                new PointF(0f, fExtentV * fScale)
            };

            // Create a homography matrix to perform the perspective transformation.
            System.Drawing.Size dimImg = new System.Drawing.Size((int)Math.Round(dstVert[1].X), (int)Math.Round(dstVert[3].Y));
            using Mat mtH = Emgu.CV.CvInvoke.FindHomography(srcVert, dstVert);

            Bitmap bms;

            // Warp the frame using the homography matrix and create a bitmap from the resulting image.
            using Mat mtWarped = new Mat();
            {
                CvInvoke.WarpPerspective(frame, mtWarped, mtH, dimImg);
                using (Image<Bgra, Byte> imgWarped = mtWarped.ToImage<Bgra, byte>(tryShareData: false))
                {
                    bms = new Bitmap(PixelFormat.Bgra8888,
                                     AlphaFormat.Opaque,
                                     imgWarped.Mat.DataPointer,
                                     new PixelSize(imgWarped.Width, imgWarped.Height),
                                     new Avalonia.Vector(96, 96),
                                     imgWarped.Mat.Step);
                }
            }

            return bms;
        }

        // Constructor for the LicensePlateViewModel class.
        public LicensePlateViewModel(AggregatedResultLPR lp)
        {
            // Ensure that the license plate candidate has at least one match.
            Debug.Assert(lp.Candidate.matches.Count > 0);

            // Set the license plate text, country code, and timestamp.
            Text = lp.Candidate.matches[0].text;
            CountryCode = lp.Candidate.matches[0].countryISO;
            TimeStamp = lp.Timestamp;

            // Crop the license plate from the video frame and assign it to the Image property.
            Image = CropLicensePlate(lp.Frame, lp.Candidate);  
        }
    }
}
