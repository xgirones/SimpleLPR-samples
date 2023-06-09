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
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Dialogs;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.DTO;
using Emgu.CV;
using Emgu.CV.Structure;
using SimpleLPR3;
using VideoANPR.Observables;
using VideoANPR.ViewModels;
using System.Reactive.Concurrency;
using System.Linq;

namespace VideoANPR.Views
{  
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        RenderTargetBitmap? bitmapCanvas_ = null;

        /// <summary>
        /// Draws license plate overlays on the supplied drawing context.
        /// </summary>
        /// <param name="ctx">The drawing context to draw overlays on.</param>
        /// <param name="resultLPR">The result containing license plate candidates.</param>
        private void DrawOverlays(DrawingContext ctx, FrameResultLPR resultLPR)
        {
            // Check if overlays are enabled and there are candidates in resultLPR
            if (this.ViewModel!.DrawOverlaysEnabled && resultLPR.Candidates != null)
            {
                // Calculate the scaling factor based on the size of the bitmap canvas and the bounds of the video canvas
                double fScaleFactor = Math.Max(bitmapCanvas_!.Size.Width / this.VideoCanvas.Bounds.Width,
                                               bitmapCanvas_!.Size.Height / this.VideoCanvas.Bounds.Height);

                // Create pens with predefined colors and scaled thicknesses
                Pen skyBluePen = new Pen(brush: new SolidColorBrush(Color.Parse("DeepSkyBlue")), thickness: 2 * fScaleFactor);
                Pen springGreenPen = new Pen(brush: new SolidColorBrush(Color.Parse("SpringGreen")), thickness: 2 * fScaleFactor);
                Color c1 = Color.Parse("Crimson");
                Color c2 = Color.Parse("Blue");

                // Iterate through each candidate in resultLPR
                foreach (Candidate lp in resultLPR.Candidates)
                {
                    // Get the plate region vertices and convert them to Avalonia Point objects
                    System.Drawing.Point[] lpv = Util.GetPlateRegionVertices(lp);
                    Point[] vertices = lpv.Append(lpv[0]).Select(x => new Point(x.X, x.Y)).ToArray();

                    // Create a polyline geometry using the vertices and draw it on the drawing context
                    PolylineGeometry gm = new PolylineGeometry(points: vertices, isFilled: false);
                    ctx.DrawGeometry(brush: new SolidColorBrush(), pen: springGreenPen, gm);

                    // Iterate through each glyph element in the candidate matches
                    foreach (Element e in lp.matches[0].elements)
                    {
                        // Draw a rectangle around the bounding box of the glyph
                        ctx.DrawRectangle(skyBluePen, new Rect(e.bbox.X, e.bbox.Y, e.bbox.Width, e.bbox.Height));

                        // Calculate the color interpolation between c1 and c2 based on the OCR confidence
                        double fLambda = e.confidence;
                        double fLambda_1 = 1.0 - fLambda;

                        double fR = (double)c1.R * fLambda + (double)c2.R * fLambda_1;
                        double fG = (double)c1.G * fLambda + (double)c2.G * fLambda_1;
                        double fB = (double)c1.B * fLambda + (double)c2.B * fLambda_1;

                        Color c3 = Color.FromRgb((byte)fR, (byte)fG, (byte)fB);

                        // Create a formatted text corresponding to the glyph and draw it beneath the element
                        FormattedText glyph =
                            new FormattedText(text: e.glyph.ToString(),
                                              typeface: new Typeface("Tahoma"),
                                              fontSize: (double)e.bbox.Height,
                                              textAlignment: TextAlignment.Center,
                                              textWrapping: TextWrapping.NoWrap,
                                              constraint: new Size(e.bbox.Width, e.bbox.Height));
                                              
                        ctx.DrawText(foreground: new SolidColorBrush(c3),
                                     origin: new Point((double)e.bbox.Left + (double)e.bbox.Width / 2.0,
                                                       (double)e.bbox.Bottom + (double)e.bbox.Height * 1.2f / 2.0f),
                                     glyph);                           
                    }
                }
            }
        }

        /// <summary>
        /// Handles the processing of a new FrameResultLPR.
        /// </summary>
        /// <param name="resultLPR">The new FrameResultLPR.</param>
        private void OnNewFrame(FrameResultLPR resultLPR)
        {
            Mat mtFrame = resultLPR.Frame;

            // Check if the bitmap canvas is null or its size doesn't match the size of the frame
            if (this.bitmapCanvas_ is null ||
                this.bitmapCanvas_.PixelSize.Width != mtFrame.Width ||
                this.bitmapCanvas_.PixelSize.Height != mtFrame.Height)
            {
                RenderTargetBitmap? bmcOld = this.bitmapCanvas_;

                // Create a new render target bitmap with the size of the frame
                this.bitmapCanvas_ = new RenderTargetBitmap(
                    pixelSize: new PixelSize(mtFrame.Width, mtFrame.Height),
                    dpi: new Vector(96, 96));

                // Schedule the update of the background image and disposal of the old bitmap to be executed on the UI thread
                RxApp.MainThreadScheduler.Schedule(_ =>
                {
                    ImageBrush imb = (ImageBrush)this.VideoCanvas.Background;
                    imb.Source = this.bitmapCanvas_;
                    bmcOld?.Dispose();
                });
            }

            using (Image<Bgra, Byte> imgFrame = mtFrame.ToImage<Bgra, byte>(tryShareData: false))
            {
                // Create a new Avalonia bitmap using the image frame data
                using (Bitmap bm = new Bitmap(PixelFormat.Bgra8888,
                                              AlphaFormat.Opaque,
                                              imgFrame.Mat.DataPointer,
                                              new PixelSize(imgFrame.Width, imgFrame.Height),
                                              new Avalonia.Vector(96, 96),
                                              imgFrame.Mat.Step))
                {
                    // Create a drawing context for the bitmap canvas
                    using (DrawingContext ctx = new(this.bitmapCanvas_.CreateDrawingContext(null)))
                    {
                        // Draw the image onto the bitmap canvas
                        ctx.DrawImage(bm, new Rect(bitmapCanvas_.Size));

                        // Schedule the drawing of overlays on the bitmap canvas to be executed on the UI thread
                        RxApp.MainThreadScheduler.Schedule(_ => DrawOverlays(ctx, resultLPR));
                    }

                    // Schedule the invalidation of the video canvas to trigger a visual update
                    RxApp.MainThreadScheduler.Schedule(_ => this.VideoCanvas.InvalidateVisual());
                }
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
            if (LB_EnabledCountries.ItemCount == 0)
            {
                List<CheckBox> items = new List<CheckBox>();

                // Iterate through the supported countries and create checkboxes for each
                for (uint i = 0; i < lpr.numSupportedCountries; i++)
                {
                    string country = lpr.get_countryCode(i);

                    CheckBox cb = new CheckBox
                    {
                        Content = country,
                        CommandParameter = country,                        
                    };

                    // Create a binding for the foreground color of the checkbox using the ViewModel's ConfigEnabledColorBrush property
                    var binding = new Binding
                    {
                        Source = this.ViewModel,
                        Path = nameof(this.ViewModel.ConfigEnabledColorBrush)
                    };

                    cb.Bind(CheckBox.ForegroundProperty, binding);

                    items.Add(cb);
                }

                // Set the items field of the enabled countries list box to the created list
                LB_EnabledCountries.Items = items;
            }

            // Ensure the number of items in the enabled countries list box matches the number of supported countries
            Debug.Assert((uint)LB_EnabledCountries.ItemCount == lpr.numSupportedCountries);

            uint idx = 0;
            foreach (object item in LB_EnabledCountries.Items)
            {
                CheckBox cb = (CheckBox)item;

                // Initialize the checkbox IsChecked property based on the country weight in the SimpleLPR engine
                cb.IsChecked = lpr.get_countryWeight(idx++) > 0.5f;

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
                async context =>
                {
                    var dlg = new OpenFileDialog()
                    {
                        AllowMultiple = false,
                        InitialFileName = context.Input.DefaultFileName,
                        Filters = context.Input.Filters,
                        Title = context.Input.Title
                    };

                    var asyncDlgRes = dlg.ShowManagedAsync(this, new ManagedFileDialogOptions { AllowDirectorySelection = false });

                    if (asyncDlgRes != null)
                    {
                        string[]? dlgRes = await asyncDlgRes;
                        context.SetOutput(dlgRes is not null && dlgRes.Length > 0 ? dlgRes[0] : string.Empty);
                    }
                    else
                    {
                        context.SetOutput(string.Empty);
                    }
                }).DisposeWith(disposables);

            // Register the interaction handler for unhandled exceptions
            SharedInteractions.UnhandledException.RegisterHandler(
                async context =>
                {
                    var msgBox = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "ErrorOccurred",
                            ContentMessage = $"Error: {context.Input.Message}",
                            Icon = MessageBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = this.Icon
                        });

                    await msgBox.Show();
                    context.SetOutput(Unit.Default);
                }).DisposeWith(disposables);

            // Register the interaction handler for confirming application exit
            SharedInteractions.ConfirmedExit.RegisterHandler(
                async context =>
                {
                    var msgBox = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.OkCancel,
                            ContentTitle = "Confirmation Request",
                            ContentMessage = context.Input,
                            Icon = MessageBox.Avalonia.Enums.Icon.Question,
                            WindowIcon = this.Icon
                        });

                    ButtonResult res = await msgBox.ShowDialog(this);

                    if (res == ButtonResult.Ok)
                    {
                        this.FinishingUp = true;
                        this.Close();
                    }

                    context.SetOutput(Unit.Default);
                }).DisposeWith(disposables);

            // Register the interaction handler for displaying video frames
            this.ViewModel?.OnNewFrame.RegisterHandler(
                context =>
                {
                    OnNewFrame(context.Input);
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
                    this.OneWayBind(this.ViewModel, vm => vm.LicensePlates, view => view.LB_DetectedPlates.Items)
                        .DisposeWith(disposables);

                    // Bind the enabled state of the input video label based on the ConfigEnabled property in the view model 
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.Label_InputVideo.Foreground,
                                    x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                        .DisposeWith(disposables);

                    // Enable or disable the input video text box based on the ConfigEnabled property in the view model 
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.TB_InputVideo.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the video path to the input video text box
                    this.Bind(this.ViewModel, vm => vm.VideoPath, view => view.TB_InputVideo.Text)
                        .DisposeWith(disposables);

                    // Bind the select video button command to the corresponding view model command
                    this.BindCommand(this.ViewModel, vm => vm.CmdSelectVideoClicked, view => view.Button_SelectVideo)
                        .DisposeWith(disposables);

                    // Bind the foreground color of the registration key label based on the RegKeyEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.RegKeyEnabled, view => view.Label_RegistrationKey.Foreground,
                                    x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                        .DisposeWith(disposables);

                    // Enable or disable the registration key text box based on the RegKeyEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.RegKeyEnabled, view => view.TB_RegistrationKey.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the registration key path to the registration key text box
                    this.Bind(this.ViewModel, vm => vm.RegistrationKeyPath, view => view.TB_RegistrationKey.Text)
                        .DisposeWith(disposables);

                    // Bind the select registration key button to the corresponding view model command
                    this.BindCommand(this.ViewModel, vm => vm.CmdSelectKeyClicked, view => view.Button_SelectKey)
                        .DisposeWith(disposables);

                    // Bind the foreground color of the enabled countries label based on the ConfigEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.Label_EnabledCountries.Foreground,
                                    x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                        .DisposeWith(disposables);

                    // Enable or disable the enabled countries list box based on the ConfigEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.LB_EnabledCountries.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the plate region detection check box to the corresponding view model property
                    this.Bind(this.ViewModel, vm => vm.PlateRegionDetectionEnabled, view => view.CB_PlateRegionDetection.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the foreground color of the plate region detection check box based on the ConfigEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_PlateRegionDetection.Foreground,
                                    x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                        .DisposeWith(disposables);

                    // Enable or disable the plate region detection check box based on the ConfigEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_PlateRegionDetection.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the crop to plate region check box to the corresponding view model property
                    this.Bind(this.ViewModel, vm => vm.CropToPlateRegionEnabled, view => view.CB_CropToPlateRegion.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the foreground color of the crop to plate region check box based on the ConfigEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_CropToPlateRegion.Foreground,
                                    x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                        .DisposeWith(disposables);

                    // Enable or disable the crop to plate region check box based on the ConfigEnabled property in the view model
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_CropToPlateRegion.IsEnabled)
                        .DisposeWith(disposables);

                    // Enable or disable the draw overlays check box based on the current playback status
                    this.OneWayBind(this.ViewModel,
                                    vm => vm.CurrentPlaybackStatus,
                                    view => view.CB_DrawOverlays.IsEnabled,
                                    x => x == MainWindowViewModel.PlaybackStatus.Playing)
                        .DisposeWith(disposables);

                    // Bind the draw overlays check box to the corresponding view model property
                    this.Bind(this.ViewModel, vm => vm.DrawOverlaysEnabled, view => view.CB_DrawOverlays.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the Play/Continue button to the corresponding view model command
                    this.BindCommand(this.ViewModel, vm => vm.CmdPlayContinueClicked, view => view.Button_PlayContinue)
                        .DisposeWith(disposables);

                    // Bind the Pause button to the corresponding view model command
                    this.BindCommand(this.ViewModel, vm => vm.CmdPauseClicked, view => view.Button_Pause)
                        .DisposeWith(disposables);

                    // Bind the Stop button to the corresponding view model command
                    this.BindCommand(this.ViewModel, vm => vm.CmdStopClicked, view => view.Button_Stop)
                        .DisposeWith(disposables);

                    // Bind the Exit button to the corresponding view model command
                    this.BindCommand(this.ViewModel, vm => vm.CmdExitClicked, view => view.Button_Exit)
                        .DisposeWith(disposables);

                    // Register the interactions used by the application
                    RegisterInteractions(disposables);

                    // Initialize the enabled countries list box
                    InitializeEnabledCountries(disposables);

                    // Bind the registration key validation to the registration key label content
                    this.BindValidation(ViewModel, vm => vm.RegistrationKeyPath, view => view.Label_RegKeyErrors.Content)
                        .DisposeWith(disposables);

                    // Handle the Closing event to prevent closing the window if the application is not finishing up
                    Observable.FromEventPattern(this, nameof(this.Closing))
                        .Do( x =>
                        {
                            System.ComponentModel.CancelEventArgs e = (System.ComponentModel.CancelEventArgs)x.EventArgs;
                            e.Cancel = !FinishingUp;
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