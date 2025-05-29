/* 
VideoANPR - Automatic Number Plate Recognition for Video Streams

VideoANPR is a sample C# application that showcases the capabilities of the SimpleLPR ANPR library for processing video streams.
It demonstrates how to leverage computer vision techniques to detect and extract license plate information in real-time.

Author: Xavier Gironés (xavier.girones@warelogic.com)

Features:
- ANPR Processing: VideoANPR utilizes the SimpleLPR ANPR library to perform automatic number plate recognition on video streams.
- Video Capture: The application uses SimpleLPR's native video capture capabilities.
- Multi-platform User Interface: VideoANPR utilizes WPF and ReactiveUI to provide a Windows user interface.

Requirements:
- .NET Core SDK 6.0 or higher
- SimpleLPR ANPR library
- WPF and ReactiveUI

Contributions and feedback are welcome! If you encounter any issues, have suggestions for improvements, or want to add new features,
please submit a pull request or open an issue on the GitHub repository.

Disclaimer: VideoANPR is intended for educational and research purposes only.
*/

using System;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using ReactiveUI;
using SimpleLPR3;
using System.Windows.Media;

namespace VideoANPR.ViewModels
{
    public class LicensePlateViewModel : ViewModelBase
    {
        // License plate information.
        public string Text { get; }
        public string CountryCode { get; }
        public TimeSpan TimeStamp { get; }

        // License plate image.
        public BitmapSource Image { get; private set; }

        // Summary of the license plate information.
        public string Summary
        {
            get => string.IsNullOrWhiteSpace(CountryCode) ?
                                            string.Format("{0:g}    {1}", TimeStamp, Text) :
                                            string.Format("{0:g}    [{1}] {2}", TimeStamp, CountryCode, Text);
        }

        // Constructor for the LicensePlateViewModel class.
        public LicensePlateViewModel(ITrackedPlateCandidate track)
        {
            var candidate = track.representativeCandidate;
            Debug.Assert(candidate.matches.Count > 0);

            Text = candidate.matches[0].text;
            CountryCode = candidate.matches[0].countryISO;
            TimeStamp = TimeSpan.FromSeconds(track.representativeTimestamp);

            // Use the representative thumbnail directly
            var thumbnail = track.representativeThumbnail;
            Debug.Assert(thumbnail != null);

            var width = (int)thumbnail.width;
            var height = (int)thumbnail.height;
            var stride = (int)thumbnail.widthStep;

            Image = BitmapSource.Create(width, height,
                                        96, 96,
                                        PixelFormats.Bgr24, null,
                                        thumbnail.data, stride * height, stride);
        }
    }
}