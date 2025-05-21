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

using System.Reactive.Disposables;
using ReactiveUI;
using Avalonia.ReactiveUI;
using VideoANPR.ViewModels;

namespace VideoANPR.Views
{
    public partial class LicensePlateView : ReactiveUserControl<LicensePlateViewModel>
    {
        // Constructor for the LicensePlateView class.
        public LicensePlateView()
        {
            InitializeComponent();

            // When the view is activated (attached to the visual tree), perform the following actions.
            this.WhenActivated(disposables =>
            {
                // One-way bind the Image property of the ViewModel to the Source property of the Image_LP control.
                // This will display the license plate image in the view.
                this.OneWayBind(this.ViewModel, vm => vm.Image, view => view.Image_LP.Source)
                    .DisposeWith(disposables);

                // One-way bind the Summary property of the ViewModel to the Content property of the Label_LP control.
                // This will display the summary of the license plate information in the view.
                this.OneWayBind(this.ViewModel, vm => vm.Summary, view => view.Label_LP.Content)                          
                    .DisposeWith(disposables);
            });
        }
    }
}
