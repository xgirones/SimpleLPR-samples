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
using System.Windows.Media;
using SimpleLPR3;
using System;
using System.Diagnostics;
using VideoANPR.Observables;
using VideoANPR.Services;
using System.Reactive.Concurrency;
using DynamicData.Binding;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

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
            public string DefaultFileName;  // Default filename
            public string DefaultExt;       // Default extension
            public string Filter;           // File filter
            public string Title;            // Title of the file dialog

            /// <summary>
            /// Initializes a new instance of the <see cref="SelectFileDialogParms"/> struct.
            /// </summary>
            /// <param name="defaultFileName">The default filename.</param>
            /// <param name="defaultExt">The default extension.</param>
            /// <param name="filter">The file filter.</param>
            /// <param name="title">The title of the file dialog.</param>
            public SelectFileDialogParms(string defaultFileName, string defaultExt, string filter, string title)
            {
                DefaultFileName = defaultFileName;
                DefaultExt = defaultExt;
                Filter = filter;
                Title = title;
            }
        }

        // Represents an interaction for selecting a file
        public static Interaction<SelectFileDialogParms, string> SelectFile { get; } = new Interaction<SelectFileDialogParms, string>();

        // Represents an interaction for selecting a folder.
        public static Interaction<string, string> SelectFolder { get; } = new Interaction<string, string>();

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
        private IProcessorPool? processorPool_ = null; // Processor pool for license plate recognition
        private IPlateCandidateTracker? plateTracker_ = null; // Tracker to track license plate candidates across video frames
        private bool trialModeEnabled_ = false;  // Flag indicating if the software is running in trial mode
        private bool validKey_ = false;  // Flag indicating if a valid registration key is provided

        private IVideoSource? videoSource_ = null;   // The video source for acquiring frames
        private IDisposable? dispSimpleLPR_ = null;  // Disposable object for handling the SimpleLPR frame pipeline

        private readonly SourceList<LicensePlateViewModel> lpList_ = new SourceList<LicensePlateViewModel>();  // Source list of detected license plate view models
        // The observable collection of license plate view models associated to lpList_
        private readonly IObservableCollection<LicensePlateViewModel> lpColl_ = new ObservableCollectionExtended<LicensePlateViewModel>();

        // Service responsible for logging tracked plates to disk.
        private readonly PlateLoggingService plateLoggingService_ = new PlateLoggingService();
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

        [Reactive]
        public bool LoggingEnabled { get; set; } = false;  // Logging is disabled by default

        [Reactive]
        public string OutputFolderPath { get; set; } = string.Empty;

        [Reactive]
        public bool OutputFolderPathValidated { get; set; } = false;  // Flag indicating if the output folder path has been validated
        #endregion

        #region OAPHs        
        public bool ConfigEnabled { [ObservableAsProperty] get; }  // Flag indicating if the controls associated to the ANPR configuration are to be enabled
        public Brush ConfigEnabledColorBrush { [ObservableAsProperty] get; } = new SolidColorBrush();   // Color brush for the configuration enabled state
        public bool RegKeyEnabled { [ObservableAsProperty] get; }  // Flag indicating if the controls associated to the registration key input are to be enabled
        public bool PlayContinueEnabled { [ObservableAsProperty] get; }  // Flag indicating if the play/continue user interface is to be enabled
        public bool LoggingConfigEnabled { [ObservableAsProperty] get; } // Flag indicating if the controls associated to candidate logging are to be enabled
        #endregion

        #region Reactive commands
        public ReactiveCommand<string, Unit> CmdCountryClicked { get; }    // Command for handling country selection
        public ReactiveCommand<Unit, Unit> CmdSelectKeyClicked { get; }    // Command for handling registration key selection
        public ReactiveCommand<Unit, Unit> CmdSelectVideoClicked { get; }  // Command for handling input video path selection
        public ReactiveCommand<Unit, Unit> CmdPlayContinueClicked { get; } // Command for handling play/continue
        public ReactiveCommand<Unit, Unit> CmdPauseClicked { get; }  // Command for handling pause
        public ReactiveCommand<Unit, Unit> CmdStopClicked { get; }   // Command for handling stop
        public ReactiveCommand<Unit, Unit> CmdExitClicked { get; }   // Command for handling user initiated application exit
        public ReactiveCommand<Unit, Unit> CmdSelectOutputFolderClicked { get; } // Command for output folder selection
        #endregion

        #region Interactions
        // An interaction that represents an event where a new frame result from the SimpleLPR pipeline is received.
        // It allows subscribers to handle the event by providing a FrameResultLPR parameter.
        public Interaction<(FrameResultLPR,bool), Unit> OnNewFrame { get; } = new Interaction<(FrameResultLPR, bool), Unit>();
        #endregion

        #region Validation context
        // A validation context object used for performing validation on data.
        public IValidationContext ValidationContext { get; } = new ValidationContext();
        #endregion

        #region Private methods
        /// <summary>
        /// Attempts to initialize the processor pool and plate tracker.
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise.</returns>
        bool TrySetupProcessors()
        {
            // Check if the SimpleLPR engine is available and the processor pool hasn't been created yet
            if (this.LPR != null && processorPool_ == null)
            {
                try
                {
                    // Create a processor pool with NUM_PROCESSORS processors
                    // This will fail if not in trial mode and no valid license key has been provided
                    processorPool_ = this.LPR.createProcessorPool(NUM_PROCESSORS);

                    // Configure the processor pool settings
                    processorPool_.plateRegionDetectionEnabled = this.PlateRegionDetectionEnabled;
                    processorPool_.cropToPlateRegionEnabled = this.CropToPlateRegionEnabled;

                    // Create a plate tracker with default parameters
                    var trackerParams = PlateCandidateTrackerSetupParms.Default;
                    plateTracker_ = this.LPR.createPlateCandidateTracker(trackerParams);
                }
                catch (Exception)
                {
                    // If initialization fails (usually due to licensing), clean up any partially created resources
                    if (processorPool_ != null)
                    {
                        processorPool_.Dispose();
                        processorPool_ = null;
                    }

                    if (plateTracker_ != null)
                    {
                        plateTracker_.Dispose();
                        plateTracker_ = null;
                    }
                }
            }

            // Update the initialization status and return it
            this.ProcessorsInitialized = processorPool_ != null && plateTracker_ != null;
            return this.ProcessorsInitialized;
        }

        /// <summary>
        /// Sets up the SimpleLPR ANPR engine and initializes the processor pool and plate tracker.
        /// </summary>
        /// <returns>A disposable object that performs cleanup actions when disposed.</returns>
        private IDisposable SetupLPR()
        {
            // Initialize engine setup parameters
            EngineSetupParms setupP;
            setupP.cudaDeviceId = -1;                    // Use CPU mode instead of GPU
            setupP.enableImageProcessingWithGPU = false; // Disable GPU for image processing
            setupP.enableClassificationWithGPU = false;  // Disable GPU for classification
            setupP.maxConcurrentImageProcessingOps = 0;  // Use default value for concurrent operations

            // Create the SimpleLPR engine instance with the specified parameters
            lpr_ = SimpleLPR.Setup(setupP);

            // Initially set the weight of all countries to 0 (disabled)
            for (uint i = 0; i < lpr_.numSupportedCountries; i++)
            {
                lpr_.set_countryWeight(i, 0);
            }

            // Try to initialize the processor pool and plate tracker
            // This will succeed without a license key during the trial period
            if (TrySetupProcessors())
            {
                trialModeEnabled_ = true;
                RegistrationKeyPath = "NO KEY REQUIRED";
            }

            // Return a disposable to clean up resources when disposed
            return Disposable.Create(
                () =>
                {
                    // Clean up the plate tracker
                    if (plateTracker_ != null)
                    {
                        plateTracker_.Dispose();
                        plateTracker_ = null;
                    }

                    // Clean up the processor pool
                    if (processorPool_ != null)
                    {
                        processorPool_.Dispose();
                        processorPool_ = null;
                    }

                    // Clean up the SimpleLPR engine
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
                .Select(x => new SolidColorBrush((Color)ColorConverter.ConvertFromString(x ? "Black" : "Gray")))
                .ToPropertyEx(this, x => x.ConfigEnabledColorBrush);

            // When the ProcessorsInitialized property changes to true,
            // set the RegKeyEnabled property to false.
            this.WhenAnyValue(x => x.ProcessorsInitialized, x => !x)
               .ToPropertyEx(this, x => x.RegKeyEnabled);

            // True when the ProcessorsInitialized is true, VideoPath and OutputFolderPathValidated are valid and the playback status is Stopped, or the playback status is Paused.
            this.WhenAnyValue(x => x.ProcessorsInitialized, y => y.VideoPath, w => w.OutputFolderPathValidated, z => z.CurrentPlaybackStatus,
                              (x, y, w, z) => (x && !string.IsNullOrWhiteSpace(y) && w && z == PlaybackStatus.Stopped) ||
                                           z == PlaybackStatus.Paused)
               .ToPropertyEx(this, x => x.PlayContinueEnabled);

            // Logging configuration is enabled when general configuration is enabled, and the logging checkbox is enabled.
            this.WhenAnyValue(x => x.ConfigEnabled, y => y.LoggingEnabled,
                             (x, y) => x && y)
                .ToPropertyEx(this, x => x.LoggingConfigEnabled);
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
                x => x.RegistrationKeyPath,            // Property to validate
                path =>
                {
                    // Path is valid if:
                    // 1. Trial mode is enabled (no key needed)
                    // 2. A valid key has already been loaded
                    // 3. The file at the specified path exists
                    return trialModeEnabled_ || validKey_ || System.IO.File.Exists(path);
                },
                "PLEASE SPECIFY A VALID PATH");        // Error message if validation fails

            // Create another validation rule for the RegistrationKeyPath property.
            // The rule checks if the key is valid so far, and if not, attempts to set the product key using the specified path.
            // If the key is valid, it sets the validKey_ flag to true.
            // If the rule fails, the error message "THE SUPPLIED KEY IS INVALID" is shown.
            this.ValidationRule(
                x => x.RegistrationKeyPath,            // Property to validate
                path =>
                {
                    // Key is valid if:
                    // 1. Trial mode is enabled (no key needed)
                    // 2. A valid key has already been loaded
                    bool bIsValidKeySoFar = trialModeEnabled_ || validKey_;

                    // If we haven't confirmed a valid key yet, try to load one
                    if (!bIsValidKeySoFar)
                    {
                        // Check if the file exists
                        bIsValidKeySoFar = System.IO.File.Exists(path);

                        if (bIsValidKeySoFar)
                        {
                            try
                            {
                                // Try to set the product key
                                lpr_?.set_productKey(path);
                                validKey_ = true;  // Mark key as valid if successful
                            } 
                            catch (Exception) 
                            {
                                // Key loading failed
                                bIsValidKeySoFar = false; 
                            }
                        }
                    }

                    return bIsValidKeySoFar;
                },
                "THE SUPPLIED KEY IS INVALID");

            // Validation rule for output folder path
            this.ValidationRule(
                vm => vm.OutputFolderPath,
                this.WhenAnyValue(
                    x => x.OutputFolderPath,
                    y => y.LoggingConfigEnabled,
                    (path, enabled) =>
                    {
                        // Path is valid if:
                        // 1. LoggingConfig is disabled (no validation needed)
                        // 2. Path points to an existing directory

                        bool bValidated = !enabled;

                        if (!bValidated)
                        {
                            try
                            {
                                // Check if directory exists
                                bValidated = Directory.Exists(path);
                            }
                            catch { }
                        }

                        OutputFolderPathValidated = bValidated;
                        return bValidated;
                    }),
                "PLEASE SPECIFY A VALID FOLDER PATH");
        }

        /// <summary>
        /// Starts the video capture process and sets up the processing pipeline.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the video source cannot be opened.</exception>
        private void StartCapture()
        {
            // Verify that the required components are properly initialized and in the expected state
            Debug.Assert(lpr_ is not null, "SimpleLPR engine must be initialized");
            Debug.Assert(processorPool_ is not null, "Processor pool must be initialized");
            Debug.Assert(plateTracker_ is not null, "Plate tracker must be initialized");
            Debug.Assert(videoSource_ is null, "Video source should be null when starting capture");
            Debug.Assert(this.dispSimpleLPR_ is null, "Processing pipeline should be null when starting capture");

            // Clear any previously detected license plates
            lpList_.Clear();

            // Apply the country weights selected in the UI to the SimpleLPR engine
            // This must be called after changing country weights and before processing
            lpr_.realizeCountryWeights();

            // Open the video source from the specified path
            // The API takes the file path, desired frame format, and optional max width/height constraints
            // Using BGR24 format and no size restrictions (-1, -1)
            videoSource_ = lpr_.openVideoSource(this.VideoPath, FrameFormat.FRAME_FORMAT_BGR24, -1, -1);

            // Verify that the video source was opened successfully
            if (videoSource_.state != VideoSourceState.VIDEO_SOURCE_STATE_OPEN)
            {
                // Clean up if opening failed
                videoSource_.Dispose();
                videoSource_ = null;

                throw new ArgumentException("Unable to open video source");
            }

            // Configure the plate logging service
            plateLoggingService_.LoggingEnabled = this.LoggingEnabled && this.OutputFolderPathValidated;
            plateLoggingService_.OutputDirectory = this.OutputFolderPath;

            // Create an observable for the pause state
            // This will emit true when paused, false when playing
            // Using WhenAnyValue to react to changes in the CurrentPlaybackStatus property
            var obPaused = this.WhenAnyValue(x => x.CurrentPlaybackStatus, x => x == PlaybackStatus.Paused);

            // Set up the video processing pipeline:
            // This reactive pipeline processes frames from the video source through several stages
            dispSimpleLPR_ = videoSource_
                .ToObservable(obPaused)                           // 1. Convert video frames to reactive stream
                .ToSimpleLPR(processorPool_)                      // 2. Process frames with ANPR engine
                .AggregateIntoRepresentatives(plateTracker_)      // 3. Track plates across multiple frames
                //.ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(
                    // OnNext handler - Process each aggregated result
                    aggregatedResult =>
                    {
                        // This blocks the producer thread until UI updates complete
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Display the frame in the UI if available
                            // This includes the original frame and any detected candidates
                            if (aggregatedResult.FrameResult != null)
                            {
                                // Use interaction to handle the frame display in the view
                                // This allows the view to update without direct coupling
                                this.OnNewFrame.Handle((aggregatedResult.FrameResult, this.DrawOverlaysEnabled)).Subscribe().Dispose();
                            }

                            // Process any new tracked license plates that have been identified
                            if (aggregatedResult.TrackerResult != null)
                            {
                                // Iterate through new tracks that met the tracking criteria
                                foreach (var track in aggregatedResult.TrackerResult.NewTracks)
                                {
                                    // Only create view models for tracks with thumbnails and valid plate text
                                    // The thumbnail should be available from the representative frame
                                    if (track.representativeThumbnail != null &&
                                        track.representativeCandidate.matches.Count > 0)
                                    {
                                        // Add the new plate to the beginning of the list
                                        // This creates a view model directly from the tracked plate
                                        lpList_.Insert(0, new LicensePlateViewModel(track));
                                    }
                                }

                                // Log the closed track asynchronously
                                // We don't await this to avoid blocking the current thread
                                Task.Run(async () =>
                                {
                                    // Handle closed tracks (these appear when tracking ends for a plate)
                                    // This is where we perform logging
                                    foreach (var track in aggregatedResult.TrackerResult.ClosedTracks)
                                    {
                                        try
                                        {
                                            await plateLoggingService_.LogTrackedPlateAsync(track, this.VideoPath);
                                        }
                                        catch (Exception ex)
                                        {
                                            // Log error but don't crash the application
                                            System.Diagnostics.Debug.WriteLine($"Failed to log tracked plate: {ex.Message}");
                                        }
                                    }

                                    // Clean up the tracker result when done
                                    // This is important to avoid memory leaks
                                    aggregatedResult.TrackerResult.Dispose();
                                });
                            }
                        });

                        // Clean up the frame result resources if available
                        // This is important to avoid memory leaks with video frames
                        if (aggregatedResult.FrameResult != null)
                        {
                            aggregatedResult.FrameResult.Frame.Dispose();
                            aggregatedResult.FrameResult.Result.Dispose();
                        }
                    },
                    // OnError handler - Handle any pipeline errors
                    ex =>
                    {
                        // Show error to the user through the interaction
                        SharedInteractions.UnhandledException.Handle(ex).Subscribe().Dispose();

                        // This ensures thread safety and synchronization with the UI
                        RxApp.MainThreadScheduler.Schedule(_ =>
                        {
                            // Stop the video capture by triggering the stop command
                            this.CmdStopClicked?.Execute().Subscribe().Dispose();
                        });
                    },
                    // OnCompleted handler - Called when the video source reaches the end
                    () =>
                    {
                        // This ensures thread safety and synchronization with the UI
                        RxApp.MainThreadScheduler.Schedule(_ =>
                        {
                            // Stop the video capture by triggering the stop command
                            this.CmdStopClicked?.Execute().Subscribe().Dispose();
                        });
                    });
        }

        /// <summary>
        /// Stops the video capture process and cleans up resources.
        /// </summary>
        private void StopCapture()
        {
            // Clean up the processing pipeline subscription
            if (dispSimpleLPR_ is not null)
            {
                // Disposing the subscription will:
                // 1. Unsubscribe from the observable
                // 2. Clean up any pending operations
                // 3. Release associated resources
                // 4. Run the cleanup code in the Disposable.Create call
                dispSimpleLPR_.Dispose();
                dispSimpleLPR_ = null;
            }

            // Clean up the video source
            if (videoSource_ is not null)
            {
                // Disposing the video source will:
                // 1. Close the video file or camera
                // 2. Release associated resources
                // 3. Free native memory
                videoSource_.Dispose();
                videoSource_ = null;
            }

            // Note: We don't dispose of the processor pool or plate tracker here
            // as they can be reused for subsequent video captures
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
                        defaultExt: ".avi",
                        filter: "Video Files (*.avi, *.mp4)|*.avi;*.mp4|All Files (*.*)|*.*",
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
                        defaultExt: ".xml",
                        filter: "XML documents (.xml)|*.xml",
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
                    Debug.Assert(videoSource_ is not null);
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

            // Command for selecting output folder
            this.CmdSelectOutputFolderClicked = ReactiveCommand.CreateFromObservable(
                () => SharedInteractions
                    .SelectFolder
                    .Handle("Select Output Folder for Logged Plates")
                    .Where(result => !string.IsNullOrWhiteSpace(result))
                    .Select(result => result.Trim())
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Do(result => this.OutputFolderPath = result)
                    .Select(result => Unit.Default),
                this.WhenAnyValue(x => x.LoggingConfigEnabled));
            #endregion

            // Activation logic for the view model.
            this.WhenActivated(disposables =>
            {
                // Monitor changes to the RegistrationKeyPath and initialize processors if a valid key exists.
                this.WhenAnyValue(x => x.RegistrationKeyPath, x => !this.trialModeEnabled_ && !string.IsNullOrWhiteSpace(x))
                    .Where(x => x)
                    // If we have a valid key or are in trial mode but processors aren't initialized, try to initialize them
                    .Do(_ => { if ((trialModeEnabled_ || validKey_) && !this.ProcessorsInitialized) TrySetupProcessors(); })
                    .Subscribe()
                    .DisposeWith(disposables);

                // Update plate region detection and crop settings for all processors.
                this.WhenAnyValue(x => x.PlateRegionDetectionEnabled, y => y.CropToPlateRegionEnabled)
                    .Do(x =>
                    {
                        if (processorPool_ != null)
                        {
                            processorPool_.plateRegionDetectionEnabled = x.Item1;
                            processorPool_.cropToPlateRegionEnabled = x.Item2;
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

                // Handle exceptions from logging commands
                this.CmdSelectOutputFolderClicked.ThrownExceptions
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