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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Dto;
using SimpleLPR3;
using VideoANPR.Observables;
using VideoANPR.ViewModels;
using System.Reactive.Concurrency;
using System.Linq;
using DynamicData;
using Avalonia.Platform.Storage;

namespace VideoANPR.Views
{  
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        RenderTargetBitmap? bitmapCanvas_ = null;

        /// <summary>
        /// Creates a geometry from text that can be scaled to fit any bounding box.
        /// </summary>
        /// <param name="text">The text to convert to geometry</param>
        /// <param name="fontFamily">Font family name</param>
        /// <param name="baseFontSize">Base font size for creating the geometry (doesn't affect final size)</param>
        /// <returns>Geometry representing the text</returns>
        private static Geometry? CreateTextGeometry(string text, string fontFamily = "Tahoma", double baseFontSize = 100)
        {
            // Create FormattedText with a reasonable base size
            // The actual size doesn't matter because we'll scale the geometry
            var formattedText = new FormattedText(
                textToFormat: text,
                foreground: Brushes.Black, // Color doesn't matter for geometry
                culture: System.Globalization.CultureInfo.InvariantCulture,
                typeface: new Typeface(fontFamily),
                flowDirection: FlowDirection.LeftToRight,
                emSize: baseFontSize);

            // Convert FormattedText to Geometry
            return formattedText.BuildGeometry(new Point(0, 0));
        }

        /// <summary>
        /// Draws text scaled to fit exactly within the specified bounding box.
        /// </summary>
        /// <param name="ctx">Drawing context</param>
        /// <param name="text">Text to draw</param>
        /// <param name="targetBounds">Target bounding box to fit the text</param>
        /// <param name="brush">Brush for drawing the text</param>
        /// <param name="preserveAspectRatio">Whether to preserve aspect ratio when scaling</param>
        /// <param name="fontFamily">Font family name</param>
        private static void DrawScaledText(
            DrawingContext ctx,
            string text,
            Rect targetBounds,
            IBrush brush,
            bool preserveAspectRatio = true,
            string fontFamily = "Tahoma")
        {
            // Skip empty text
            if (string.IsNullOrWhiteSpace(text))
                return;

            // Create geometry from text
            var textGeometry = CreateTextGeometry(text, fontFamily);
            if (textGeometry == null)
                return;

            // Get the bounds of the original geometry
            var originalBounds = textGeometry.Bounds;

            // Skip if geometry has no bounds (shouldn't happen with normal text)
            if (originalBounds.Width <= 0 || originalBounds.Height <= 0)
                return;

            // Calculate scale factors to fit the target bounds
            double scaleX = targetBounds.Width / originalBounds.Width;
            double scaleY = targetBounds.Height / originalBounds.Height;

            // If preserving aspect ratio, use the smaller scale factor for both dimensions
            if (preserveAspectRatio)
            {
                double uniformScale = Math.Min(scaleX, scaleY);
                scaleX = uniformScale;
                scaleY = uniformScale;
            }

            // Create transformation matrix
            var transform = Matrix.Identity;

            // Step 1: Translate to origin (remove original bounds offset)
            transform = transform.Append(Matrix.CreateTranslation(-originalBounds.X, -originalBounds.Y));

            // Step 2: Scale to fit target size
            transform = transform.Append(Matrix.CreateScale(scaleX, scaleY));

            // Step 3: Calculate final position
            // If preserving aspect ratio, center the scaled text within the target bounds
            double finalWidth = originalBounds.Width * scaleX;
            double finalHeight = originalBounds.Height * scaleY;
            double offsetX = targetBounds.X + (targetBounds.Width - finalWidth) / 2;
            double offsetY = targetBounds.Y + (targetBounds.Height - finalHeight) / 2;

            // Step 4: Translate to final position
            transform = transform.Append(Matrix.CreateTranslation(offsetX, offsetY));

            // Apply transformation and draw
            using (ctx.PushTransform(transform))
            {
                ctx.DrawGeometry(brush, null, textGeometry);
            }
        }

        /// <summary>
        /// Draws license plate overlays on the video frame to visualize detections.
        /// </summary>
        /// <param name="ctx">The drawing context to draw on.</param>
        /// <param name="resultLPR">The frame result containing the detected license plates.</param>
        private void DrawOverlays(DrawingContext ctx, FrameResultLPR resultLPR)
        {
            // Only draw overlays if:
            // 1. The bitmap canvas exists
            // 2. Overlay drawing is enabled in the view model
            // 3. There are license plate candidates in the result
            if (this.bitmapCanvas_ != null &&
                this.ViewModel!.DrawOverlaysEnabled &&
                resultLPR.Result.candidates != null)
            {
                // Calculate the scaling factor for the drawing
                // This ensures consistent line thickness regardless of video size
                double fScaleFactor = Math.Max(
                    bitmapCanvas_!.Size.Width / this.VideoCanvas.Bounds.Width,
                    bitmapCanvas_!.Size.Height / this.VideoCanvas.Bounds.Height);

                // Create pens with predefined colors and scaled thicknesses
                // Sky blue for character bounding boxes
                Pen skyBluePen = new Pen(
                    brush: new SolidColorBrush(Color.Parse("DeepSkyBlue")),
                    thickness: 2 * fScaleFactor);

                // Spring green for plate region boundaries
                Pen springGreenPen = new Pen(
                    brush: new SolidColorBrush(Color.Parse("SpringGreen")),
                    thickness: 2 * fScaleFactor);

                // Define colors for confidence visualization
                // These will be interpolated based on character confidence
                Color c1 = Color.Parse("Crimson");    // Low confidence color
                Color c2 = Color.Parse("Blue");       // High confidence color

                // Process each license plate candidate in the result
                foreach (Candidate lp in resultLPR.Result.candidates)
                {
                    // Get the vertices of the plate region
                    // These may come from plate detection or the text bounding box
                    System.Drawing.Point[] lpv = Util.GetPlateRegionVertices(lp);

                    // Convert to Avalonia Point array and append first point to close the polygon
                    Point[] vertices = lpv.Append(lpv[0])
                                          .Select(x => new Point(x.X, x.Y))
                                          .ToArray();

                    // Create a polyline geometry for the plate region
                    PolylineGeometry gm = new PolylineGeometry(
                        points: vertices,
                        isFilled: false);

                    // Draw the plate region outline
                    ctx.DrawGeometry(brush: null, pen: springGreenPen, geometry: gm);

                    // Process the first match if available (best match)
                    if (lp.matches.Count > 0)
                    {
                        // Iterate through each character element in the match
                        foreach (Element e in lp.matches[0].elements)
                        {
                            // Draw a rectangle around the bounding box of the character
                            ctx.DrawRectangle(
                                pen: skyBluePen,
                                rect: new Rect(e.bbox.X, e.bbox.Y, e.bbox.Width, e.bbox.Height));

                            // Calculate the color interpolation between c1 and c2 based on OCR confidence
                            double fLambda = e.confidence;     // Confidence value between 0 and 1
                            double fLambda_1 = 1.0 - fLambda;  // Inverse for blending

                            // Interpolate RGB components
                            double fR = (double)c1.R * fLambda + (double)c2.R * fLambda_1;
                            double fG = (double)c1.G * fLambda + (double)c2.G * fLambda_1;
                            double fB = (double)c1.B * fLambda + (double)c2.B * fLambda_1;

                            // Create the interpolated color
                            Color c3 = Color.FromRgb((byte)fR, (byte)fG, (byte)fB);

                            Color confidenceColor = Color.FromRgb((byte)fR, (byte)fG, (byte)fB);
                            var textBrush = new SolidColorBrush(confidenceColor);

                            // Define the target bounds for the character text
                            // Position it slightly below the character bounding box
                            var textBounds = new Rect(
                                x: e.bbox.X,
                                y: e.bbox.Bottom + e.bbox.Height * 0.1, // 10% spacing below bbox
                                width: e.bbox.Width,
                                height: e.bbox.Height * 0.8); // Make text 80% of character height

                            // Draw the character using scaled geometry
                            DrawScaledText(
                                ctx: ctx,
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
        private void OnNewFrame(FrameResultLPR resultLPR)
        {
            // Get the video frame from the result
            var videoFrame = resultLPR.Frame;

            // Check if we need to create or resize the bitmap canvas
            // This happens when:
            // 1. This is the first frame (bitmapCanvas_ is null)
            // 2. The frame size has changed
            if (this.bitmapCanvas_ is null ||
                this.bitmapCanvas_.PixelSize.Width != videoFrame.width ||
                this.bitmapCanvas_.PixelSize.Height != videoFrame.height)
            {
                // Store the old bitmap canvas for proper disposal after switch
                RenderTargetBitmap? bmcOld = this.bitmapCanvas_;

                // Create a new render target bitmap with the size of the current frame
                // This bitmap serves as the drawing surface for the video display
                this.bitmapCanvas_ = new RenderTargetBitmap(
                    pixelSize: new PixelSize((int)videoFrame.width, (int)videoFrame.height),
                    dpi: new Vector(96, 96));  // Standard DPI

                // Schedule UI updates to be executed on the UI thread
                // This ensures thread safety when manipulating UI elements
                RxApp.MainThreadScheduler.Schedule(_ =>
                {
                    // Update the background image of the video canvas
                    ImageBrush? imb = this.VideoCanvas.Background as ImageBrush;
                    if (imb != null)
                    {
                        imb.Source = this.bitmapCanvas_;
                    }

                    // Dispose the old bitmap canvas to prevent memory leaks
                    // This must be done on the UI thread after the switch is complete
                    bmcOld?.Dispose();
                });
            }

            // Create a bitmap directly from the video frame data
            // Note: The SimpleLPR IVideoFrame provides direct memory access
            Bitmap bitmap = new Bitmap(
                PixelFormats.Bgr24,                           // Use BGR24 format (matches SimpleLPR's output)
                AlphaFormat.Opaque,                           // No alpha channel needed
                videoFrame.data,                              // Direct access to frame data
                new PixelSize((int)videoFrame.width,          // Width from the frame
                             (int)videoFrame.height),         // Height from the frame
                new Avalonia.Vector(96, 96),                  // Standard DPI
                (int)videoFrame.widthStep);                   // Row stride from the frame (bytes per row)

            // Schedule drawing operations to be executed on the UI thread
            // This ensures thread safety and synchronization with the UI
            RxApp.MainThreadScheduler.Schedule(_ =>
            {
                // Use a using statement to ensure the bitmap is disposed
                // after it's drawn to the canvas
                using (Bitmap bm = bitmap)
                {
                    // Create a drawing context for the bitmap canvas
                    // This context allows drawing operations to be performed
                    using (DrawingContext ctx = this.bitmapCanvas_.CreateDrawingContext())
                    {
                        // Draw the video frame image onto the canvas
                        // This copies the pixel data into the render target
                        ctx.DrawImage(bm, new Rect(bitmapCanvas_.Size));

                        // Draw license plate overlays if enabled
                        // This adds visual indicators for detected plates and text
                        DrawOverlays(ctx, resultLPR);

                        // Invalidate the video canvas to trigger a visual update
                        // This ensures the UI shows the new frame
                        this.VideoCanvas.InvalidateVisual();
                    }
                }
            });
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
                LB_EnabledCountries.ItemsSource = items;
            }

            // Ensure the number of items in the enabled countries list box matches the number of supported countries
            Debug.Assert((uint)LB_EnabledCountries.ItemCount == lpr.numSupportedCountries);

            uint idx = 0;
            foreach (object? item in LB_EnabledCountries.Items)
            {
                CheckBox? cb = item as CheckBox;
                if (cb == null) continue;

                // Initialize the check box IsChecked property based on the country weight in the SimpleLPR engine
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
                    var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                    if (storageProvider is null) throw new NullReferenceException("Unable to obtain StorageProvider");

                    FilePickerOpenOptions op = new FilePickerOpenOptions()
                    {
                        AllowMultiple = false,
                        Title = context.Input.Title,
                        FileTypeFilter = context.Input.Filters,
                        SuggestedStartLocation = await storageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents)
                    };

                    var asyncDlgRes = storageProvider.OpenFilePickerAsync(op);

                    if (asyncDlgRes != null)
                    {
                        IReadOnlyList<IStorageFile>? dlgRes = await asyncDlgRes;
                        string? filePath = dlgRes is not null && dlgRes.Count > 0 ? dlgRes[0].Path.AbsolutePath : null;
                        context.SetOutput(filePath ?? string.Empty);
                    }
                    else
                    {
                        context.SetOutput(string.Empty);
                    }
                }).DisposeWith(disposables);

            // Register the interaction handler for selecting a folder
            SharedInteractions.SelectFolder.RegisterHandler(
                async context =>
                {
                    var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                    if (storageProvider is null) throw new NullReferenceException("Unable to obtain StorageProvider");

                    FolderPickerOpenOptions op = new FolderPickerOpenOptions()
                    {
                        AllowMultiple = false,
                        Title = context.Input,
                        SuggestedStartLocation = await storageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents)
                    };

                    var asyncDlgRes = storageProvider.OpenFolderPickerAsync(op);

                    if (asyncDlgRes != null)
                    {
                        IReadOnlyList<IStorageFolder>? dlgRes = await asyncDlgRes;
                        string? folderPath = dlgRes is not null && dlgRes.Count > 0 ? dlgRes[0].Path.AbsolutePath : null;
                        context.SetOutput(folderPath ?? string.Empty);
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
                    var msgBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.Ok,
                            ContentTitle = "ErrorOccurred",
                            ContentMessage = $"Error: {context.Input.Message}",
                            Icon = MsBox.Avalonia.Enums.Icon.Error,
                            WindowIcon = this.Icon!
                        });

                    await msgBox.ShowAsync();
                    context.SetOutput(Unit.Default);
                }).DisposeWith(disposables);

            // Register the interaction handler for confirming application exit
            SharedInteractions.ConfirmedExit.RegisterHandler(
                async context =>
                {
                    var msgBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = ButtonEnum.OkCancel,
                            ContentTitle = "Confirmation Request",
                            ContentMessage = context.Input,
                            Icon = MsBox.Avalonia.Enums.Icon.Question,
                            WindowIcon = this.Icon!
                        });

                    ButtonResult res = await msgBox.ShowWindowDialogAsync(this);

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
                    this.OneWayBind(this.ViewModel, vm => vm.LicensePlates, view => view.LB_DetectedPlates.ItemsSource)
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

                    // Bind the checkbox state to the LoggingEnabled property in the view model
                    // This two-way binding allows the user to enable/disable logging
                    // The checkbox state will reflect the current logging setting and update it when clicked
                    this.Bind(this.ViewModel, vm => vm.LoggingEnabled, view => view.CB_EnableLogging.IsChecked)
                        .DisposeWith(disposables);

                    // Bind the checkbox enabled state to the ConfigEnabled property
                    // This ensures the logging checkbox is only interactive when configuration changes are allowed
                    // (i.e., when video is not currently playing/paused)
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_EnableLogging.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the checkbox foreground color to indicate when it's configurable
                    // Same pattern as other configuration UI elements: Gray when locked, Black when configurable
                    // This provides consistent visual feedback across all configuration controls
                    this.OneWayBind(this.ViewModel, vm => vm.ConfigEnabled, view => view.CB_EnableLogging.Foreground,
                                    x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                        .DisposeWith(disposables);

                    // Bind the output folder label foreground color to indicate configuration state
                    // Maintains visual consistency with other configuration labels
                    // Users can immediately see which settings are currently locked vs configurable
                    this.OneWayBind(this.ViewModel, vm => vm.LoggingConfigEnabled, view => view.Label_OutputFolder.Foreground,
                                    x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                        .DisposeWith(disposables);

                    // Bind the output folder path text box to the OutputFolderPath property
                    // This two-way binding allows users to:
                    // 1. See the currently selected output folder path
                    // 2. Manually edit the path if desired (though Browse button is preferred)
                    // 3. Have changes automatically synchronized with the view model
                    this.Bind(this.ViewModel, vm => vm.OutputFolderPath, view => view.TB_OutputFolder.Text)
                        .DisposeWith(disposables);

                    // Bind the output folder text box enabled state to the LoggingConfigEnabled property
                    // Prevents users from editing the output folder path during video playback
                    // This maintains data integrity and prevents mid-playback configuration changes
                    this.OneWayBind(this.ViewModel, vm => vm.LoggingConfigEnabled, view => view.TB_OutputFolder.IsEnabled)
                        .DisposeWith(disposables);

                    // Bind the "Browse..." button to the folder selection command
                    // This command will open a folder picker dialog when clicked
                    // The command's CanExecute is automatically bound to LoggingConfigEnabled in the view model
                    this.BindCommand(this.ViewModel, vm => vm.CmdSelectOutputFolderClicked, view => view.Button_SelectOutputFolder)
                        .DisposeWith(disposables);

                    // Register the interactions used by the application
                    RegisterInteractions(disposables);

                    // Initialize the enabled countries list box
                    InitializeEnabledCountries(disposables);

                    // Bind the registration key validation to the registration key label content
                    this.BindValidation(ViewModel, vm => vm.RegistrationKeyPath, view => view.Label_RegKeyErrors.Content)
                        .DisposeWith(disposables);

                    // Bind validation error display for the output folder path
                    // This will show validation messages (e.g., "PLEASE SPECIFY A VALID FOLDER PATH")
                    // The error text appears in red below the output folder controls
                    // Validation runs automatically when the OutputFolderPath property changes
                    this.BindValidation(ViewModel, vm => vm.OutputFolderPath, view => view.Label_OutputFolderErrors.Content)
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