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
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleLPR3;
using VideoANPR.Observables;
using VideoANPR.ViewModels;
using System.Reactive.Concurrency;
using System.Linq;
using DynamicData;
using Microsoft.Win32;
using System.IO;
using System.CodeDom;

namespace VideoANPR.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IViewFor<MainWindowViewModel>
    {
        WriteableBitmap? frameBuffer_ = null;

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                "ViewModel",
                typeof(MainWindowViewModel),
                typeof(MainWindow),
                new PropertyMetadata(null));

        public MainWindowViewModel? ViewModel
        {
            get => (MainWindowViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        object? IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (MainWindowViewModel?)value;
        }

        /// <summary>
        /// Draws text scaled to fit exactly within the specified bounding box.
        /// </summary>
        /// <param name="dc">Drawing context</param>
        /// <param name="text">Text to draw</param>
        /// <param name="targetBounds">Target bounding box to fit the text</param>
        /// <param name="brush">Brush for drawing the text</param>
        /// <param name="preserveAspectRatio">Whether to preserve aspect ratio when scaling</param>
        /// <param name="fontFamily">Font family name</param>
        private static void DrawScaledText(
            DrawingContext dc,
            string text,
            Rect targetBounds,
            Brush brush,
            bool preserveAspectRatio = true,
            string fontFamily = "Tahoma")
        {
            // Skip empty text
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Create formatted text with a base size
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily),
                targetBounds.Height * 0.8, // Use target height directly instead of fixed 100
                brush,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            // Center the text in the target bounds
            double x = targetBounds.X + (targetBounds.Width - formattedText.Width) / 2;
            double y = targetBounds.Y + (targetBounds.Height - formattedText.Height) / 2;

            // Draw the text directly without complex transformations
            dc.DrawText(formattedText, new Point(x, y));
        }

        /// <summary>
        /// Draws license plate overlays on the video frame to visualize detections.
        /// </summary>
        /// <param name="dc">The drawing context to draw on.</param>
        /// <param name="resultLPR">The frame result containing the detected license plates.</param>
        private void DrawOverlays(DrawingContext dc, FrameResultLPR resultLPR)
        {
            // Only draw overlays if:
            // 1. The bitmap canvas exists
            // 2. There is a list of license plate candidates in the result
            if (this.frameBuffer_ != null &&
                resultLPR.Result.candidates != null)
            {
                // Calculate the scaling factor for the drawing
                // This ensures consistent line thickness regardless of video size
                double fScaleFactor = Math.Max(
                    frameBuffer_!.PixelWidth / this.VideoCanvas.ActualWidth,
                    frameBuffer_!.PixelHeight / this.VideoCanvas.ActualHeight);

                // Create pens with predefined colors and scaled thicknesses
                // Sky blue for character bounding boxes
                Pen skyBluePen = new Pen(
                    brush: new SolidColorBrush(Colors.DeepSkyBlue),
                    thickness: 2 * fScaleFactor);

                // Spring green for plate region boundaries
                Pen springGreenPen = new Pen(
                    brush: new SolidColorBrush(Colors.SpringGreen),
                    thickness: 2 * fScaleFactor);

                // Define colors for confidence visualization  
                // These will be interpolated based on character confidence
                Color c1 = Colors.Crimson;    // High confidence color
                Color c2 = Colors.Blue;       // low confidence color

                // Process each license plate candidate in the result
                foreach (Candidate lp in resultLPR.Result.candidates)
                {
                    // Get the vertices of the plate region
                    // These may come from plate detection or the text bounding box
                    System.Drawing.Point[] lpv = Util.GetPlateRegionVertices(lp);

                    // Convert to WPF Point array
                    Point[] vertices = lpv.Select(p => new Point(p.X, p.Y)).ToArray();

                    // Create a polyline for the plate region
                    PathFigure figure = new PathFigure
                    {
                        StartPoint = vertices[0],
                        IsClosed = true
                    };

                    foreach (var vertex in vertices.Skip(1))
                    {
                        figure.Segments.Add(new LineSegment(vertex, true));
                    }

                    PathGeometry geometry = new PathGeometry();
                    geometry.Figures.Add(figure);

                    // Draw the plate region outline
                    dc.DrawGeometry(brush: null, pen: springGreenPen, geometry: geometry);

                    // Process the first match if available (best match)
                    if (lp.matches.Count > 0)
                    {
                        // Iterate through each character element in the match
                        foreach (Element e in lp.matches[0].elements)
                        {
                            // Draw a rectangle around the bounding box of the character
                            dc.DrawRectangle(
                                brush: null,
                                pen: skyBluePen,
                                rectangle: new Rect(e.bbox.X, e.bbox.Y, e.bbox.Width, e.bbox.Height));

                            // Calculate the color interpolation between c1 and c2 based on OCR confidence
                            double fLambda = e.confidence;     // Confidence value between 0 and 1
                            double fLambda_1 = 1.0 - fLambda;  // Inverse for blending

                            // Interpolate RGB components
                            double fR = (double)c1.R * fLambda + (double)c2.R * fLambda_1;
                            double fG = (double)c1.G * fLambda + (double)c2.G * fLambda_1;
                            double fB = (double)c1.B * fLambda + (double)c2.B * fLambda_1;

                            Color confidenceColor = Color.FromRgb((byte)fR, (byte)fG, (byte)fB);
                            var textBrush = new SolidColorBrush(confidenceColor);

                            // Define the target bounds for the character text
                            // Position it slightly below the character bounding box
                            var textBounds = new Rect(
                                x: e.bbox.X,
                                y: e.bbox.Bottom + e.bbox.Height * 0.1, // 10% spacing below bbox
                                width: e.bbox.Width,
                                height: e.bbox.Height * 0.8); // Make text 80% of character height

                            // Draw the character using scaled text
                            DrawScaledText(
                                dc: dc,
                                text: e.glyph.ToString(),
                                targetBounds: textBounds,
                                brush: textBrush,
                                preserveAspectRatio: true, // Keep character proportions
                                fontFamily: "Tahoma");
                        }                    
                    }
                }
            }
        }

        /// <summary>
        /// Handles the processing of a new video frame for display.
        /// </summary>
        /// <param name="resultLPR">The frame result containing the video frame and detection results.</param>
        private void OnNewFrame(FrameResultLPR resultLPR, bool bDrawOverlays)
        {
            // Get the video frame from the result
            var videoFrame = resultLPR.Frame;

            // Check if we need to create or resize the frame buffer
            if (this.frameBuffer_ is null ||
                this.frameBuffer_.PixelWidth != videoFrame.width ||
                this.frameBuffer_.PixelHeight != videoFrame.height)
            {
                // Create a new writable bitmap for the frame data
                this.frameBuffer_ = new WriteableBitmap(
                    (int)videoFrame.width,
                    (int)videoFrame.height,
                    96, 96,
                    PixelFormats.Bgr24,
                    null);
            }

            // Update the frame buffer with video data
            this.frameBuffer_.WritePixels(
                new Int32Rect(0, 0, (int)videoFrame.width, (int)videoFrame.height),
                videoFrame.data,
                (int)videoFrame.widthStep * (int)videoFrame.height,
                (int)videoFrame.widthStep,
                0, 0);

            // If overlays are disabled, just display the bitmap directly
            if (!bDrawOverlays)
            {
                ImageBrush? imb = this.VideoCanvas.Background as ImageBrush;
                if (imb != null)
                {
                    imb.ImageSource = this.frameBuffer_;
                }

                return;
            }

            // Create drawing without rendering to bitmap
            var drawingGroup = new DrawingGroup();
            using (var dc = drawingGroup.Open())
            {
                dc.DrawImage(frameBuffer_, new Rect(0, 0, frameBuffer_.PixelWidth, frameBuffer_.PixelHeight));
                DrawOverlays(dc, resultLPR);
            }

            // Use DrawingImage instead of RenderTargetBitmap
            var drawingImage = new DrawingImage(drawingGroup);

            ImageBrush? imb2 = this.VideoCanvas.Background as ImageBrush;
            if (imb2 != null)
            {
                imb2.ImageSource = drawingImage;
            }
        }

        /// <summary>
        /// Initializes the list of enabled countries.
        /// </summary>
        /// <param name="disposables">The composite disposable to track and dispose of resources.</param>
        private void InitializeEnabledCountries(CompositeDisposable disposables)
        {
            Debug.Assert(ViewModel != null);

            // Ensure the ViewModel is not null
            ISimpleLPR? lpr = ViewModel.LPR;
            Debug.Assert(lpr != null);

            // If the enabled countries list box is empty, populate it
            if (LB_EnabledCountries.Items.IsEmpty)
            {
                // Iterate through the supported countries and create checkboxes for each
                for (uint i = 0; i < lpr.numSupportedCountries; i++)
                {
                    string country = lpr.get_countryCode(i);

                    CheckBox cb = new CheckBox
                    {
                        Content = country,
                        CommandParameter = country,
                        Margin = new Thickness(2),
                        MinWidth = 80
                    };

                    // Create a binding for the foreground color
                    cb.SetBinding(CheckBox.ForegroundProperty, "ConfigEnabledColorBrush");

                    LB_EnabledCountries.Items.Add(cb);
                }
            }

            // Ensure the number of items in the enabled countries list box matches the number of supported countries
            Debug.Assert((uint)LB_EnabledCountries.Items.Count == lpr.numSupportedCountries);

            for (uint i = 0; i < lpr.numSupportedCountries; i++)
            {
                CheckBox cb = (CheckBox)LB_EnabledCountries.Items[(int)i];
                
                // Initialize the check box IsChecked property based on the country weight
                cb.IsChecked = lpr.get_countryWeight(i) > 0.5f;

                // Set up the command for handling country clicks
                cb.Command = ViewModel.CmdCountryClicked;
                Disposable.Create(() => cb.Command = null).DisposeWith(disposables);
            }
        }

        // Gets or sets a value indicating whether the application is finishing up.
        private bool FinishingUp { get; set; } = false;

        /// <summary>
        /// Registers the interactions used by the application.
        /// </summary>
        /// <param name="disposables">The composite disposable to track and dispose of resources.</param>
        private void RegisterInteractions(CompositeDisposable disposables)
        {
            // Register the interaction handler for selecting a file
            SharedInteractions.SelectFile.RegisterHandler(
                context =>
                {
                    var dlg = new OpenFileDialog()
                    {
                        FileName = context.Input.DefaultFileName,
                        DefaultExt = context.Input.DefaultExt,
                        Filter = context.Input.Filter,
                        Title = context.Input.Title
                    };

                    bool? bSelected = dlg.ShowDialog();

                    if (bSelected != null && bSelected == true)
                    {
                        context.SetOutput(dlg.FileName);
                    }
                    else
                    { 
                        context.SetOutput(string.Empty);
                    }    
                }).DisposeWith(disposables);

            // Register the interaction handler for selecting a folder
            SharedInteractions.SelectFolder.RegisterHandler(
                context =>
                {
                    var dlg = new System.Windows.Forms.FolderBrowserDialog()
                    {
                        Description = context.Input,
                        ShowNewFolderButton = true
                    };

                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        context.SetOutput(dlg.SelectedPath);
                    }
                    else
                    {
                        context.SetOutput(string.Empty);
                    }
                }).DisposeWith(disposables);

            // Register the interaction handler for unhandled exceptions
            SharedInteractions.UnhandledException.RegisterHandler(
                context =>
                {
                    MessageBox.Show($"Error: {context.Input.Message}", "Error Occurred", MessageBoxButton.OK, MessageBoxImage.Error);
                    context.SetOutput(Unit.Default);
                }).DisposeWith(disposables);

            // Register the interaction handler for confirming application exit
            SharedInteractions.ConfirmedExit.RegisterHandler(
                context =>
                {
                    if (MessageBox.Show(context.Input, "Confirmation Request", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                    {
                        this.FinishingUp = true;
                        RxApp.MainThreadScheduler.Schedule(() => this.Close());
                        //this.Close();
                    }
                    context.SetOutput(Unit.Default);
                }).DisposeWith(disposables);

            // Register the interaction handler for displaying video frames
            this.ViewModel?.OnNewFrame.RegisterHandler(
                context =>
                {
                    OnNewFrame(context.Input.Item1, context.Input.Item2);
                    context.SetOutput(Unit.Default);
                }
                ).DisposeWith(disposables);
        }

        /// <summary>
        /// Constructor for the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new ViewModels.MainWindowViewModel();

            this.DataContext = ViewModel;

            // When the main window is activated, perform the following actions
            this.WhenActivated(disposables =>
            {
                ISimpleLPR? lpr = ViewModel.LPR;

                if (lpr != null)
                {
                    // Set the focus to the main window once the processor list has been initialized
                    this.ViewModel.WhenAnyValue(x => x.ProcessorsInitialized)
                        .Do(_ => this.Focus())                        
                        .Subscribe()
                        .DisposeWith(disposables);

                    // Bind the detected license plates collection to the LB_DetectedPlates list box
                    this.OneWayBind(this.ViewModel, vm => vm.LicensePlates, view => view.LB_DetectedPlates.ItemsSource)
                        .DisposeWith(disposables);

                    // Bind the enabled state of the input video label
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.Label_InputVideo.IsEnabled)
                        .DisposeWith(disposables);

                    // Enable or disable the input video text box
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.TB_InputVideo.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the video path to the input video text box
                    this.Bind(this.ViewModel, vm => vm.VideoPath, view => view.TB_InputVideo.Text)
                        .DisposeWith(disposables);

                    // Bind the select video button command
                    this.BindCommand(this.ViewModel, vm => vm.CmdSelectVideoClicked, view => view.Button_SelectVideo)
                        .DisposeWith(disposables);

                    // Bind the enabled state of the registration key label
                    this.OneWayBind(this.ViewModel, vm => vm.RegKeyEnabled, view => view.Label_RegistrationKey.IsEnabled)
                        .DisposeWith(disposables);

                    // Enable or disable the registration key text box
                    this.OneWayBind(this.ViewModel, vm => vm.RegKeyEnabled, view => view.TB_RegistrationKey.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the registration key path
                    this.Bind(this.ViewModel, vm => vm.RegistrationKeyPath, view => view.TB_RegistrationKey.Text)
                        .DisposeWith(disposables);

                    // Bind the select registration key button
                    this.BindCommand(this.ViewModel, vm => vm.CmdSelectKeyClicked, view => view.Button_SelectKey)
                        .DisposeWith(disposables);

                    // Bind the enabled state of the enabled countries label
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.Label_EnabledCountries.IsEnabled)
                        .DisposeWith(disposables);

                    // Enable or disable the enabled countries list box
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.LB_EnabledCountries.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the plate region detection check box
                    this.Bind(this.ViewModel, vm => vm.PlateRegionDetectionEnabled, view => view.CB_PlateRegionDetection.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the foreground color of the plate region detection check box
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabledColorBrush, view => view.CB_PlateRegionDetection.Foreground)
                        .DisposeWith(disposables);

                    // Enable or disable the plate region detection check box
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_PlateRegionDetection.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the crop to plate region check box
                    this.Bind(this.ViewModel, vm => vm.CropToPlateRegionEnabled, view => view.CB_CropToPlateRegion.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the foreground color of the crop to plate region check box
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabledColorBrush, view => view.CB_CropToPlateRegion.Foreground)
                        .DisposeWith(disposables);

                    // Enable or disable the crop to plate region check box
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_CropToPlateRegion.IsEnabled)
                        .DisposeWith(disposables);

                    // Enable or disable the draw overlays check box based on the current playback status
                    this.OneWayBind(this.ViewModel,
                                    vm => vm.CurrentPlaybackStatus,
                                    view => view.CB_DrawOverlays.IsEnabled,
                                    x => x == MainWindowViewModel.PlaybackStatus.Playing)
                        .DisposeWith(disposables);

                    // Bind the draw overlays check box
                    this.Bind(this.ViewModel, vm => vm.DrawOverlaysEnabled, view => view.CB_DrawOverlays.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the Play/Continue button
                    this.BindCommand(this.ViewModel, vm => vm.CmdPlayContinueClicked, view => view.Button_PlayContinue)
                        .DisposeWith(disposables);

                    // Bind the Play/Continue button text based on playback status
                    this.OneWayBind(this.ViewModel,
                        vm => vm.CurrentPlaybackStatus,
                        view => view.Button_PlayContinue.Content,
                        status => status == MainWindowViewModel.PlaybackStatus.Stopped ? "Play" : "Continue")
                        .DisposeWith(disposables);

                    // Bind the Pause button
                    this.BindCommand(this.ViewModel, vm => vm.CmdPauseClicked, view => view.Button_Pause)
                        .DisposeWith(disposables);

                    // Bind the Stop button
                    this.BindCommand(this.ViewModel, vm => vm.CmdStopClicked, view => view.Button_Stop)
                        .DisposeWith(disposables);

                    // Bind the Exit button
                    this.BindCommand(this.ViewModel, vm => vm.CmdExitClicked, view => view.Button_Exit)
                        .DisposeWith(disposables);

                    // Bind the checkbox state to the LoggingEnabled property
                    this.Bind(this.ViewModel, vm => vm.LoggingEnabled, view => view.CB_EnableLogging.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the checkbox enabled state
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_EnableLogging.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the checkbox foreground color
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabledColorBrush, view => view.CB_EnableLogging.Foreground)
                        .DisposeWith(disposables);

                    // Bind the output folder label enabled state
                    this.OneWayBind(this.ViewModel, vm => vm.LoggingConfigEnabled, view => view.Label_OutputFolder.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the output folder path text box
                    this.Bind(this.ViewModel, vm => vm.OutputFolderPath, view => view.TB_OutputFolder.Text)
                        .DisposeWith(disposables);

                    // Bind the output folder text box enabled state
                    this.OneWayBind(this.ViewModel, vm => vm.LoggingConfigEnabled, view => view.TB_OutputFolder.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the "Browse..." button
                    this.BindCommand(this.ViewModel, vm => vm.CmdSelectOutputFolderClicked, view => view.Button_SelectOutputFolder)
                        .DisposeWith(disposables);

                    // Register the interactions used by the application
                    RegisterInteractions(disposables);

                    // Initialize the enabled countries list box
                    InitializeEnabledCountries(disposables);

                    // Bind the registration key validation
                    this.BindValidation(ViewModel, vm => vm.RegistrationKeyPath, view => view.Label_RegKeyErrors.Content)
                        .DisposeWith(disposables);

                    // Bind validation error display for the output folder path
                    this.BindValidation(ViewModel, vm => vm.OutputFolderPath, view => view.Label_OutputFolderErrors.Content)
                        .DisposeWith(disposables);

                    // Handle the Closing event
                    this.Events().Closing
                        .Do(x =>
                        {
                            x.Cancel = !FinishingUp;
                        })
                        .Where(_ => !FinishingUp)
                        .Select(_ => Unit.Default)
                        .InvokeCommand(this.ViewModel.CmdExitClicked)
                        .DisposeWith(disposables);

                    // Set the window title to include the version number of the SimpleLPR library
                    this.Title = "SimpleLPR Video Capture Demo " +
                                 $" - v.{lpr.versionNumber.A}.{lpr.versionNumber.B}.{lpr.versionNumber.C}.{lpr.versionNumber.D}";
                }
            });
        }
    }
}