/* 
VideoANPR - Automatic Number Plate Recognition for Video Streams

VideoANPR is a sample C# application that showcases the capabilities of the SimpleLPR ANPR library for processing video streams.
It demonstrates how to leverage computer vision techniques to detect and extract license plate information in real-time.

Author: Xavier Gironés (xavier.girones@warelogic.com)

Features:
- ANPR Processing: VideoANPR utilizes the SimpleLPR ANPR library to perform automatic number plate recognition on video streams.
- Video Capture: The application uses SimpleLPR's native video capture capabilities.
- Multi-platform User Interface: VideoANPR utilizes Avalonia and ReactiveUI to provide a cross-platform user interface,
  enabling the application to run on both Windows and Linux systems seamlessly.

Requirements:
- .NET Core SDK 6.0 or higher
- SimpleLPR ANPR library
- Avalonia and ReactiveUI

Contributions and feedback are welcome! If you encounter any issues, have suggestions for improvements, or want to add new features,
please submit a pull request or open an issue on the GitHub repository.

Disclaimer: VideoANPR is intended for educational and research purposes only.
*/

using System;
using System.Diagnostics;
using Avalonia.Platform;
using SimpleLPR3;

using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace VideoANPR.ViewModels
{
    public class LicensePlateViewModel : ViewModelBase
    {
        // License plate information.
        public string Text { get; }
        public string CountryCode { get; }
        public TimeSpan TimeStamp { get; }

        // License plate image.
        public Bitmap Image { get; private set; }

        // Summary of the license plate information.
        public string Summary
        {
            get => string.IsNullOrWhiteSpace(CountryCode) ?
                                            string.Format("{0:g}    {1}", TimeStamp, Text) :
                                            string.Format("{0:g}    [{1}] {2}", TimeStamp, CountryCode, Text);
        }

        // Constructor for the LicensePlateViewModel class.
        //
        // The caller provides the thumbnail and RETAINS OWNERSHIP of it: this constructor
        // copies the pixel data into a bitmap and never stores the wrapper, so the caller
        // can (and should) dispose it right after construction -- see the 'using' in
        // MainWindowViewModel. Fetching 'track.representativeThumbnail' in here instead
        // would create a second native wrapper that somebody else would have to dispose.
        public LicensePlateViewModel(ITrackedPlateCandidate track, IVideoFrame thumbnail)
        {
            var candidate = track.representativeCandidate;
            Debug.Assert(candidate.matches.Count > 0);
            Debug.Assert(thumbnail != null);

            Text = candidate.matches[0].text;
            CountryCode = candidate.matches[0].countryISO;
            TimeStamp = TimeSpan.FromSeconds(track.representativeTimestamp);

            var width = (int)thumbnail.width;
            var height = (int)thumbnail.height;
            var stride = (int)thumbnail.widthStep;

            // The Avalonia Bitmap constructor COPIES the pixels out of the native buffer
            // into its own memory, so the bitmap remains valid after the caller disposes
            // the thumbnail.
            Image = new Bitmap(PixelFormats.Bgr24,
                               AlphaFormat.Opaque,
                               thumbnail.data,
                               new Avalonia.PixelSize(width, height),
                               new Avalonia.Vector(96, 96),
                               stride);
        }
    }
}
