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

using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;
using ReactiveUI.Validation.Extensions;
using DynamicData;
using Avalonia;
using Avalonia.Media;
using SimpleLPR3;
using System;
using System.Diagnostics;
using VideoANPR.Observables;
using System.Reactive.Concurrency;
using DynamicData.Binding;
using Avalonia.Controls;
using System.ComponentModel;

namespace VideoANPR.ViewModels
{
    /// <summary>
    /// Provides shared interactions for file selection, handling unhandled exceptions, and confirming program exit.
    /// </summary>
    public static class SharedInteractions
    {
        /// <summary>
        /// Represents the parameters for selecting a file in a file dialog.
        /// </summary>
        public struct SelectFileDialogParms
        {
            public string DefaultFileName;          // Default file name for the file dialog
            public List<FileDialogFilter> Filters;  // List of file filters for the file dialog
            public string Title;                    // Title of the file dialog

            /// <summary>
            /// Initializes a new instance of the <see cref="SelectFileDialogParms"/> struct.
            /// </summary>
            /// <param name="defaultFileName">The default file name for the file dialog.</param>
            /// <param name="filters">The list of file filters for the file dialog.</param>
            /// <param name="title">The title of the file dialog.</param>
            public SelectFileDialogParms(string defaultFileName, List<FileDialogFilter> filters, string title)
            {
                DefaultFileName = defaultFileName;
                Filters = filters;
                Title = title;
            }
        }

        // Represents an interaction for selecting a file
        public static Interaction<SelectFileDialogParms, string> SelectFile { get; } = new Interaction<SelectFileDialogParms, string>();

        // Represents an interaction for handling unhandled exceptions
        public static Interaction<Exception, Unit> UnhandledException { get; } = new Interaction<Exception, Unit>();

        // Represents an interaction for confirming program exit
        public static Interaction<string, Unit> ConfirmedExit { get; } = new Interaction<string, Unit>();
    }

    /// <summary>
    /// Represents the view model for the main window.
    /// </summary>
    public class MainWindowViewModel : ViewModelBase, IActivatableViewModel, IValidatableViewModel
    {
        private const uint NUM_PROCESSORS = 4;
        public enum PlaybackStatus
        {
            Stopped,
            Playing,
            Paused
        }

        #region Backing fields and private stuff      
        private ISimpleLPR? lpr_ = null;  // Instance of ISimpleLPR for license plate recognition
        private readonly List<IProcessor> processors_ = new List<IProcessor>(); // List of SimpleLPR processors for license plate recognition
        private bool trialModeEnabled_ = false;  // Flag indicating if the software is running in trial mode
        private bool validKey_ = false;  // Flag indicating if a valid registration key is provided

        private Emgu.CV.VideoCapture? videoCapture_ = null;  // Video capture device
        private IDisposable? dispSimpleLPR_ = null;  // Disposable object for handling the SimpleLPR frame pipeline
        private IDisposable? dispPlateAggr_ = null;  // Disposable object for handling license plate aggregation pipeline

        private readonly SourceList<LicensePlateViewModel> lpList_ = new SourceList<LicensePlateViewModel>();  // Source list of detected license plate view models
        // The observable collection of license plate view models associated to lpList_
        private readonly IObservableCollection<LicensePlateViewModel> lpColl_ = new ObservableCollectionExtended<LicensePlateViewModel>();
        #endregion

        #region Normal properties
        public ViewModelActivator Activator { get; }  // Activator for the view model

        public ISimpleLPR? LPR { get => lpr_; }  // Property for accessing the ISimpleLPR instance

        public IObservableCollection<LicensePlateViewModel> LicensePlates => lpColl_; // The collection of detected license plate view models     
        #endregion

        #region Reactive properties
        [Reactive]
        public bool ProcessorsInitialized { get; set; } = false;  // Flag indicating if the processor pool has been successfully initialized

        [Reactive]
        public bool PlateRegionDetectionEnabled { get; set; } = true;  // Flag indicating if the plate region detection feature is enabled

        [Reactive]
        public bool CropToPlateRegionEnabled { get; set; } = false;  // Flag indicating if cropping to plate region is enabled

        [Reactive]
        public bool DrawOverlaysEnabled { get; set; } = true;  // Flag indicating if license plate overlays should be drawn on each frame

        [Reactive]
        public PlaybackStatus CurrentPlaybackStatus { get; set; } = PlaybackStatus.Stopped;  // Current playback status

        [Reactive]
        public string VideoPath { get; set; } = string.Empty;  // Path to the video file

        [Reactive]
        public string RegistrationKeyPath { get; set; } = string.Empty;   // Path to the registration key file
        #endregion

        #region OAPHs        
        public bool ConfigEnabled { [ObservableAsProperty] get; }  // Flag indicating if the controls associated to the ANPR configuration are to be enabled
        public Brush ConfigEnabledColorBrush { [ObservableAsProperty] get; } = new SolidColorBrush();   // Color brush for the configuration enabled state
        public bool RegKeyEnabled { [ObservableAsProperty] get; }  // Flag indicating if the controls associated to the registration key input are to be enabled
        public bool PlayContinueEnabled { [ObservableAsProperty] get; }  // Flag indicating if the play/continue user interface is to be enabled
        #endregion

        #region Reactive commands
        public ReactiveCommand<string, Unit> CmdCountryClicked { get; }    // Command for handling country selection
        public ReactiveCommand<Unit, Unit> CmdSelectKeyClicked { get; }    // Command for handling registration key selection
        public ReactiveCommand<Unit, Unit> CmdSelectVideoClicked { get; }  // Command for handling input video path selection
        public ReactiveCommand<Unit, Unit> CmdPlayContinueClicked { get; } // Command for handling play/continue
        public ReactiveCommand<Unit, Unit> CmdPauseClicked { get; }  // Command for handling pause
        public ReactiveCommand<Unit, Unit> CmdStopClicked { get; }   // Command for handling stop
        public ReactiveCommand<Unit, Unit> CmdExitClicked { get; }   // Command for handling user initiated application exit
        #endregion

        #region Interactions
        // An interaction that represents an event where a new frame result from the SimpleLPR pipeline is received.
        // It allows subscribers to handle the event by providing a FrameResultLPR parameter.
        public Interaction<FrameResultLPR, Unit> OnNewFrame { get; } = new Interaction<FrameResultLPR, Unit>();
        #endregion

        #region Validation context
        // A validation context object used for performing validation on data.
        public ValidationContext ValidationContext { get; } = new ValidationContext();
        #endregion

        #region Private methods
        /// <summary>
        /// Tries to set up the processors for license plate recognition.
        /// </summary>
        /// <returns><c>true</c> if the processors were successfully initialized, <c>false</c> otherwise.</returns>
        bool TrySetupProcessors()
        {
            if (this.LPR != null)
            {
                try
                {
                    while ((uint)processors_.Count < NUM_PROCESSORS)
                    {
                        // Attempt to create a new processor instance. Throws an exception if a registration key is required.
                        IProcessor proc = this.LPR.createProcessor();

                        // Set the plate region detection and crop settings for the processor.
                        proc.plateRegionDetectionEnabled = this.PlateRegionDetectionEnabled;
                        proc.cropToPlateRegionEnabled = this.CropToPlateRegionEnabled;

                        // Add the created processor to the processors pool.
                        processors_.Add(proc);
                    }
                }
                catch (Exception)
                {
                    // Ignore the exception, typically raised when a registration key is required.
                }
            }

            // Check if the number of initialized processors matches the desired number.
            this.ProcessorsInitialized = (uint)processors_.Count == NUM_PROCESSORS;

            // Return whether the processors were successfully initialized.
            return this.ProcessorsInitialized;
        }

        /// <summary>
        /// Sets up the SimpleLPR ANPR engine.
        /// </summary>
        /// <returns>A disposable object that performs cleanup actions when disposed.</returns>
        private IDisposable SetupLPR()
        {
            // Setup the ANPR engine

            EngineSetupParms setupP;
            setupP.cudaDeviceId = -1; // Select CPU
            setupP.enableImageProcessingWithGPU = false;
            setupP.enableClassificationWithGPU = false;
            setupP.maxConcurrentImageProcessingOps = 0;  // Use the default value. 

            // Setup the ANPR engine with the specified parameters.
            lpr_ = SimpleLPR.Setup(setupP);

            // Set the weight of supported countries to 0 initially.
            for (uint i = 0; i < lpr_.numSupportedCountries; i++)
            {
                lpr_.set_countryWeight(i, 0);
            }

            // Try to setup processors without providing a registration key, and enable trial mode if successful.
            if (TrySetupProcessors())
            {
                trialModeEnabled_ = true;
                RegistrationKeyPath = "NO KEY REQUIRED";
            }

            // Return a disposable object that performs cleanup actions when disposed.
            return Disposable.Create(
                () =>
                {
                    // Dispose all the processors and clear the list.
                    foreach (IProcessor proc in processors_)
                    {
                        proc.Dispose();
                    }
                    processors_.Clear();

                    // Dispose the ANPR engine and set its reference to null.
                    lpr_.Dispose();
                    lpr_ = null;
                });
        }

        /// <summary>
        /// Handles the click event on a country in the user interface.
        /// </summary>
        /// <param name="country">The country code.</param>
        private void OnCountryClicked(string country)
        {
            // Toggle the weight of the clicked country between 0 and 1.
            lpr_?.set_countryWeight(country, 1.0f - lpr_.get_countryWeight(country));
        }

        /// <summary>
        /// Initialize Observable As Property Helpers (OAPHs).
        /// </summary>
        private void InitializeOAPHs()
        {
            // True When ProcessorsInitialized is true and the CurrentPlaybackStatus is Stopped.
            this.WhenAnyValue(x => x.ProcessorsInitialized, y => y.CurrentPlaybackStatus, (x, y) => x && y == PlaybackStatus.Stopped)
                .ToPropertyEx(this, x => x.ConfigEnabled);

            // When the ConfigEnabled property changes,
            // create a SolidColorBrush based on its value ("Black" if true, "Gray" if false),
            // and set the ConfigEnabledColorBrush property to the resulting brush.
            this.WhenAnyValue(x => x.ConfigEnabled)
                .Select(x => new SolidColorBrush(Color.Parse(x ? "Black" : "Gray")))
                .ToPropertyEx(this, x => x.ConfigEnabledColorBrush);

            // When the ProcessorsInitialized property changes to true,
            // set the RegKeyEnabled property to false.
            this.WhenAnyValue(x => x.ProcessorsInitialized, x => !x)
               .ToPropertyEx(this, x => x.RegKeyEnabled);

            // True when the ProcessorsInitialized is true, VideoPath is valid and the playback status is Stopped, or the playback status is Paused.
            this.WhenAnyValue(x => x.ProcessorsInitialized, y => y.VideoPath, z => z.CurrentPlaybackStatus,
                              (x, y, z) => (x && !string.IsNullOrWhiteSpace(y) && z == PlaybackStatus.Stopped) ||
                                           z == PlaybackStatus.Paused)
               .ToPropertyEx(this, x => x.PlayContinueEnabled);
        }

        /// <summary>
        /// Initialize validation rules.
        /// </summary>
        private void InitializeValidations()
        {
            // Create a validation rule for the RegistrationKeyPath property.
            // The rule checks if trial mode is enabled or a valid key exists at the specified path.
            // If the rule fails, the error message "PLEASE SPECIFY A VALID PATH" is shown.
            this.ValidationRule(
                x => x.RegistrationKeyPath,
                path =>
                {
                    return trialModeEnabled_ || validKey_ || System.IO.File.Exists(path);
                },
                "PLEASE SPECIFY A VALID PATH");

            // Create another validation rule for the RegistrationKeyPath property.
            // The rule checks if the key is valid so far, and if not, attempts to set the product key using the specified path.
            // If the key is valid, it sets the validKey_ flag to true.
            // If the rule fails, the error message "THE SUPPLIED KEY IS INVALID" is shown.
            this.ValidationRule(
                x => x.RegistrationKeyPath,
                path =>
                {
                    bool bIsValidKeySoFar = trialModeEnabled_ || validKey_;

                    if (!bIsValidKeySoFar)
                    {
                        bIsValidKeySoFar = System.IO.File.Exists(path);

                        if (bIsValidKeySoFar)
                        {
                            try
                            {
                                lpr_?.set_productKey(path);
                                validKey_ = true;
                            } 
                            catch (Exception) 
                            { 
                                bIsValidKeySoFar = false; 
                            }
                        }
                    }

                    return bIsValidKeySoFar;
                },
                "THE SUPPLIED KEY IS INVALID");
        }

        /// <summary>
        /// Starts the video capture process.
        /// </summary>
        private void StartCapture()
        {
            // Assert that the necessary objects are not null before starting the capture.
            Debug.Assert(lpr_ is not null);
            Debug.Assert(videoCapture_ is null);
            Debug.Assert(this.dispSimpleLPR_ is null);

            // Clear the license plate list and realize the country weights selected through the UI.
            lpList_.Clear();
            lpr_.realizeCountryWeights();

            // Create a new video capture using the specified VideoPath.
            videoCapture_ = new Emgu.CV.VideoCapture(this.VideoPath, Emgu.CV.VideoCapture.API.Ffmpeg);

            // Check if the video capture is successfully opened.
            if (!videoCapture_.IsOpened)
            {
                // Dispose the video capture object and set it to null.
                videoCapture_.Dispose();
                videoCapture_ = null;

                // Throw an ArgumentException indicating the inability to open the video source.
                throw new ArgumentException("Unable to open video source");
            }

            // Create observables for pause status and SimpleLPR processing.
            var obPaused = this.WhenAnyValue(x => x.CurrentPlaybackStatus, x => x == PlaybackStatus.Paused);
            var obSimpleLPR = this.videoCapture_
                .ToObservable(scheduler: new EventLoopScheduler(), obPaused: obPaused)
                .ToSimpleLPR(pcs: this.processors_, scheduler: RxApp.TaskpoolScheduler, bExhaustive: true)
                .ObserveOn(new EventLoopScheduler())
                .Publish()
                .RefCount();

            // Set up the subscription for processing individual frames.
            this.dispSimpleLPR_ = obSimpleLPR
                .Catch(Observable.Empty<FrameResultLPR>()) // Swallow any incoming exceptions since they will be processed by the 'long' branch of the pipeline.                
                .Do(elem =>
                    {
                        // Handle the new frame using the OnNewFrame interaction.
                        this.OnNewFrame.Handle(elem).Subscribe().Dispose();
                    })
                .Subscribe(
                    // Elements already handled in the previous stage.
                    elem => { },
                    // Exception handler, used only for exceptions arising in the previous stage.
                    ex =>
                    {
                        // Handle the unhandled exception using the SharedInteractions.UnhandledException interaction.
                        SharedInteractions.UnhandledException.Handle(ex).Subscribe().Dispose();

                        // Execute the CmdStopClicked command programmatically.
                        this.CmdStopClicked?.Execute().Subscribe().Dispose();
                    });

            // Set up the subscription for aggregating license plate results.
            this.dispPlateAggr_ = obSimpleLPR
                .AggregateIntoRepresentatives(triggerWindow: TimeSpan.FromSeconds(3),
                                              maxIdleTime: TimeSpan.FromSeconds(2),
                                              disposeInputFrames: true,
                                              discardNonLPCandidates: this.PlateRegionDetectionEnabled || this.CropToPlateRegionEnabled)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(elem =>
                    {
                        // Create a new LicensePlateViewModel for the aggregated result and add it to the license plate list.
                        lpList_.Insert(0, new LicensePlateViewModel(elem));
                        elem.Frame.Dispose();
                    })
                .Subscribe(
                    // Elements already handled in the previous stage.
                    elem => { },
                    // Exception handler
                    ex =>
                    {
                        // Handle the unhandled exception using the SharedInteractions.UnhandledException interaction.
                        SharedInteractions.UnhandledException.Handle(ex).Subscribe().Dispose();

                        // Execute the CmdStopClicked command programmatically.
                        this.CmdStopClicked?.Execute().Subscribe().Dispose();
                    },
                    // Completion handler
                    () =>
                    {
                        // Execute the CmdStopClicked command programmatically.
                        this.CmdStopClicked?.Execute().Subscribe().Dispose();
                    });
        }

        /// <summary>
        /// Ends the capture process.
        /// </summary>
        private void StopCapture()
        {
            // Dispose the dispPlateAggr_ subscription if it exists.
            if (dispPlateAggr_ is not null)
            {
                dispPlateAggr_.Dispose();
                dispPlateAggr_ = null;
            }

            // Dispose the dispSimpleLPR_ subscription if it exists.
            if (dispSimpleLPR_ is not null)
            {
                dispSimpleLPR_.Dispose();
                dispSimpleLPR_ = null;
            }

            // Dispose the videoCapture_ object if it exists.
            if (videoCapture_ is not null)
            {
                videoCapture_.Dispose();
                videoCapture_ = null;
            }
        }
        #endregion

        /// <summary>
        /// Constructor for the MainWindowViewModel class.
        /// </summary>
        public MainWindowViewModel()
        {
            // Create a ViewModelActivator to control the activation and deactivation of the view model.
            this.Activator = new ViewModelActivator();

            // Initialize the observables, aggregations, properties, and handlers.
            InitializeOAPHs();
            InitializeValidations();

            #region Commands initialization
            // Command for handling country selection.
            this.CmdCountryClicked = ReactiveCommand.Create<string>(x => OnCountryClicked(x));

            // Command for selecting a video file.
            this.CmdSelectVideoClicked = ReactiveCommand.CreateFromObservable(
                () => SharedInteractions
                    .SelectFile
                    .Handle(new SharedInteractions.SelectFileDialogParms(
                        defaultFileName: "video", 
                        filters: new List<FileDialogFilter>{
                                        new FileDialogFilter{ Name="Video Files", Extensions=new List<string>{"mp4", "avi"} },
                                        new FileDialogFilter{ Name="All Files", Extensions=new List<string>{"*"} } },
                        title: "Select Video File"))
                    .Where(result => !string.IsNullOrWhiteSpace(result))
                    .Select(result => result.Trim())
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Do(result => this.VideoPath = result)
                    .Select(result => Unit.Default),
                this.WhenAnyValue(x => x.ConfigEnabled));

            // Command for selecting a license key file.
            this.CmdSelectKeyClicked = ReactiveCommand.CreateFromObservable(
                () => SharedInteractions
                    .SelectFile
                    .Handle(new SharedInteractions.SelectFileDialogParms(
                        defaultFileName: "key",
                        filters: new List<FileDialogFilter>{
                                        new FileDialogFilter{ Name="XML documents", Extensions=new List<string>{"xml"} },
                                        new FileDialogFilter{ Name="All Files", Extensions=new List<string>{"*"} } },
                        title: "Select License Key File"))
                    .Where(result => !string.IsNullOrWhiteSpace(result))
                    .Select(result => result.Trim())
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Do(result => RegistrationKeyPath = result)
                    .Select(result => Unit.Default),
                this.WhenAnyValue(x => x.RegKeyEnabled));

            // Command for starting or continuing the video capture.
            this.CmdPlayContinueClicked = ReactiveCommand.Create(() =>
                {
                    if (this.CurrentPlaybackStatus == PlaybackStatus.Stopped)
                    {
                        StartCapture();
                    }

                    this.CurrentPlaybackStatus = PlaybackStatus.Playing;
                },
                this.WhenAnyValue(x => x.PlayContinueEnabled));

            // Command for pausing the video capture.
            this.CmdPauseClicked = ReactiveCommand.Create(() =>
                {
                    this.CurrentPlaybackStatus = PlaybackStatus.Paused;
                },
                this.WhenAnyValue(x => x.CurrentPlaybackStatus, x => x == PlaybackStatus.Playing));
            
            // Command for stopping the video capture.
            this.CmdStopClicked = ReactiveCommand.Create(() =>
                {
                    Debug.Assert(videoCapture_ is not null);
                    Debug.Assert(this.dispSimpleLPR_ is not null);

                    StopCapture();

                    this.CurrentPlaybackStatus = PlaybackStatus.Stopped;
                },
                this.WhenAnyValue(x => x.CurrentPlaybackStatus, x => x == PlaybackStatus.Playing || x == PlaybackStatus.Paused));

            // Command for confirming application exit.
            this.CmdExitClicked = ReactiveCommand.CreateFromObservable(
                () => SharedInteractions
                    .ConfirmedExit
                    .Handle("Are you sure you want to quit the application?"));
            #endregion

            // Activation logic for the view model.
            this.WhenActivated(disposables =>
            {
                // Monitor changes to the RegistrationKeyPath and initialize processors if a valid key exists.
                this.WhenAnyValue(x => x.RegistrationKeyPath, x => !this.trialModeEnabled_ && !string.IsNullOrWhiteSpace(x))
                    .Where(x => x)
                    .Do(_ => { if ((trialModeEnabled_ || validKey_) && !this.ProcessorsInitialized) TrySetupProcessors(); })
                    .Subscribe()
                    .DisposeWith(disposables);

                // Update plate region detection and crop settings for all processors.
                this.WhenAnyValue(x => x.PlateRegionDetectionEnabled, y => y.CropToPlateRegionEnabled)
                    .Do(x =>
                    {
                        foreach (IProcessor proc in this.processors_)
                        {
                            proc.plateRegionDetectionEnabled = x.Item1;
                            proc.cropToPlateRegionEnabled = x.Item2;
                        }
                    })
                    .Subscribe()
                    .DisposeWith(disposables);

                // Handle exceptions thrown by CmdSelectKeyClicked.
                this.CmdSelectKeyClicked.ThrownExceptions
                    .SelectMany(ex => SharedInteractions.UnhandledException.Handle(ex))
                    .Subscribe()
                    .DisposeWith(disposables);

                // Handle exceptions thrown by CmdPlayContinueClicked.
                this.CmdPlayContinueClicked.ThrownExceptions
                    .SelectMany(ex => SharedInteractions.UnhandledException.Handle(ex))
                    .Subscribe()
                    .DisposeWith(disposables);

                // Make sure that the video capture is stopped when deactivating the view model.
                Disposable.Create(() => StopCapture()).DisposeWith(disposables);

                // Connect the license plate list to the observable collection and bind it to the view.
                lpList_.Connect()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Bind(lpColl_)
                    .Subscribe()
                    .DisposeWith(disposables);

                // Setup the SimpleLPR engine.
                SetupLPR().DisposeWith(disposables);
            });
        }
    }
}