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

using System.Reactive.Disposables;
using System.Windows.Controls;
using ReactiveUI;
using VideoANPR.ViewModels;

namespace VideoANPR.Views
{
    /// <summary>
    /// Interaction logic for LicensePlateView.xaml
    /// </summary>
    public partial class LicensePlateView : UserControl, IViewFor<LicensePlateViewModel>
    {
        public static readonly System.Windows.DependencyProperty ViewModelProperty =
            System.Windows.DependencyProperty.Register(
                "ViewModel",
                typeof(LicensePlateViewModel),
                typeof(LicensePlateView),
                new System.Windows.PropertyMetadata(null));

        public LicensePlateViewModel? ViewModel
        {
            get => (LicensePlateViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (LicensePlateViewModel?)value;
        }

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