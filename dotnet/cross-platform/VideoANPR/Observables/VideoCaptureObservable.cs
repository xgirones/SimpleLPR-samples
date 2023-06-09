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
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace VideoANPR.Observables
{
    public static class VideoCaptureObservableExtension
    {
        /// <summary>
        /// Converts a VideoCapture object to an IObservable of TimeInterval<Emgu.CV.Mat>.
        /// </summary>
        /// <param name="videoCapture">The VideoCapture object to convert.</param>
        /// <param name="obPaused">Optional observable for pausing functionality.</param>
        /// <param name="scheduler">Optional scheduler to control concurrency.</param>
        /// <returns>An IObservable of TimeInterval<Emgu.CV.Mat>.</returns>
        public static IObservable<TimeInterval<Emgu.CV.Mat>> ToObservable(
            this Emgu.CV.VideoCapture videoCapture,            
            IObservable<bool>? obPaused = null,
            IScheduler? scheduler = null)
        {
            // Check if the videoCapture is opened
            if (!videoCapture.IsOpened)
                throw new ArgumentException($"{nameof(videoCapture)} is not opened");

            // If scheduler is null, use an EventLoopScheduler for concurrency control
            if (scheduler is null) 
                scheduler = new EventLoopScheduler();

            // Wrap the videoCapture in a lockable disposable to ensure proper cleanup
            var videoCaptureWrapper = videoCapture.LockableDisposableWrap();

            // Create the captureObservable using Observable.Create to handle custom observable behavior
            var captureObservable = Observable.Create<TimeInterval<Emgu.CV.Mat>>(o =>
            {
                // Initialize variables for frame time tracking and completion status
                TimeSpan curFrameTime =
                    videoCaptureWrapper.Inner is null ?
                        TimeSpan.Zero :
                        TimeSpan.FromMilliseconds(videoCaptureWrapper.Inner.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosMsec));

                TimeSpan lastFrameDuration = TimeSpan.Zero;

                bool bCompleted = false;
                bool bIsPaused = false;

                // Method to query a frame from the video source and handle exceptions
                Action<Action> queryFrame = (self) =>
                {                    
                    Exception? ex = null;
                    Emgu.CV.Mat? frame = null;                                       

                    try
                    {
                        lock (videoCaptureWrapper.Lock)
                        {
                            if (videoCaptureWrapper.Inner != null)
                            {
                                frame = videoCaptureWrapper.Inner.QueryFrame();
                                bCompleted = frame is null;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }

                    if (frame != null)
                    {
                        TimeSpan frameTime = curFrameTime;

                        // Update the current frame time
                        curFrameTime = TimeSpan.FromMilliseconds(videoCaptureWrapper.Inner!.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosMsec)); // Warranted to be non null.

                        if (curFrameTime == TimeSpan.Zero)
                        {
                            // To deal with OpenCV issue https://github.com/opencv/opencv/issues/8763
                            // Adjust frame time using the last frame duration
                            curFrameTime = frameTime + lastFrameDuration;
                        }
                        else
                        {
                            // Calculate the duration of the current frame
                            lastFrameDuration = curFrameTime - frameTime;
                        }

                        // Emit the frame with its time information to the observer
                        o.OnNext(new TimeInterval<Emgu.CV.Mat>(frame, frameTime));

                        // If not paused, continue querying frames by recursively invoking queryFrame
                        if (!bIsPaused)
                        {
                            self();
                        }
                    }
                    // If completed, signal completion to the observer
                    else if (bCompleted)
                    {
                        o.OnCompleted();
                    }
                    // If an exception occurred, signal the error to the observer
                    else if (ex != null)
                    {
                        o.OnError(ex);
                    }
                };

                // Create a CompositeDisposable to manage multiple subscriptions and resources
                CompositeDisposable disposables = new CompositeDisposable();

                // Subscribe to obPaused to handle the pausing functionality
                var sbPaused = obPaused?
                 .Subscribe(
                    bPause =>
                    {
                        // If pause state hasn't changed, return
                        if (bPause == bIsPaused) return;

                        bIsPaused = bPause;

                        // If not paused and not completed, schedule queryFrame
                        if (!bPause && !bCompleted)
                        {
                            disposables.Add(scheduler.Schedule(queryFrame));
                        }
                    });

                // Add the subscription to sbPaused to the list of disposables if not null
                if (sbPaused != null)
                    disposables.Add(sbPaused);

                // Schedule the initial queryFrame to start capturing frames
                disposables.Add(scheduler.Schedule(queryFrame));

                // Return the CompositeDisposable to clean up resources when the observable is disposed
                return disposables;
            });

            // Return the captureObservable, disposing the videoCaptureWrapper when done
            return Observable.Using(() => videoCaptureWrapper, _ => captureObservable);
        }
    }
}
