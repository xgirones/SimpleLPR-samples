/* 
VideoANPR - Automatic Number Plate Recognition for Video Streams

VideoANPR is a sample C# application that showcases the capabilities of the SimpleLPR ANPR library for processing video streams.
It demonstrates how to leverage computer vision techniques to detect and extract license plate information in real-time.

Author: Xavier Gironés (xavier.girones@warelogic.com)

Features:
- ANPR Processing: VideoANPR utilizes the SimpleLPR ANPR library to perform automatic number plate recognition on video streams.
- Video Capture: The application uses SimpleLPR's native video capture capabilities, replacing the previous Emgu.CV dependency.
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
using System.Reactive.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using SimpleLPR3;

namespace VideoANPR.Observables
{
    public static class VideoCaptureObservableExtension
    {
        private static IObservable<IVideoFrame> CreateCaptureObservable(
            IVideoSource videoSource,
            IObservable<bool>? obPaused,
            IScheduler scheduler)
        {
            return Observable.Create<IVideoFrame>(o =>
            {
                // Initialize variables for completion status and paused state
                bool bCompleted = false;
                bool bIsPaused = false;
                bool bUnsubscribed = false;

                // Method to query a frame from the video source and handle exceptions
                Action<Action> queryFrame = (self) =>
                {
                    if (bCompleted) return;

                    try
                    {
                        // Get the next frame from the video source
                        var frame = videoSource.nextFrame();

                        if (frame != null)
                        {
                            // Emit the frame to the observer
                            o.OnNext(frame);

                            // If not paused and while subscribed, continue querying frames by recursively invoking queryFrame
                            if (!bIsPaused && !bUnsubscribed)
                            {
                                self();
                            }
                        }
                        else
                        {
                            // No frame means we've reached the end
                            bCompleted = true;
                            o.OnCompleted();
                        }
                    }
                    catch (Exception e)
                    {
                        // If an exception occurred, signal the error to the observer
                        bCompleted = true;
                        o.OnError(e);
                    }
                };

                // Subscribe to obPaused to handle the pausing functionality
                IDisposable? sbPaused = null;
                if (obPaused != null)
                {
                    sbPaused = obPaused.Subscribe(
                        bPause =>
                        {
                            // If pause state hasn't changed, return
                            if (bPause == bIsPaused) return;

                            bIsPaused = bPause;

                            // If resumed and not completed, schedule queryFrame
                            if (!bPause && !bCompleted && !bUnsubscribed)
                            {
                                scheduler.Schedule(queryFrame);
                            }
                        });
                }

                scheduler.Schedule(queryFrame);

                // Return a disposable that cleans up
                return Disposable.Create(() =>
                {
                    sbPaused?.Dispose();
                    bUnsubscribed = true;
                });
            });
        }

        /// <summary>
        /// Converts an IVideoSource object to an IObservable of IVideoFrame.
        /// </summary>
        /// <param name="videoSource">The IVideoSource object to convert.</param>
        /// <param name="obPaused">Optional observable for pausing functionality.</param>
        /// <param name="scheduler">Optional scheduler to control concurrency. If null, creates a new EventLoopScheduler.</param>
        /// <returns>An IObservable of IVideoFrame.</returns>
        /// <remarks>
        /// Note: The caller is responsible for disposing the videoSource.
        /// If no scheduler is provided, a new EventLoopScheduler is created and will be disposed when all subscriptions are disposed.
        /// </remarks>
        public static IObservable<IVideoFrame> ToObservable(
            this IVideoSource videoSource,
            IObservable<bool>? obPaused = null,
            IScheduler? scheduler = null)
        {
            // Check if the videoSource is in a valid state
            if (videoSource.state != VideoSourceState.VIDEO_SOURCE_STATE_OPEN)
                throw new ArgumentException($"{nameof(videoSource)} is not in OPEN state");

            // If scheduler is null, create one and manage its lifetime
            if (scheduler is null)
            {
                return Observable.Using(
                    () => new EventLoopScheduler(),
                    createdScheduler => CreateCaptureObservable(videoSource, obPaused, createdScheduler)
                );
            }
            else
            {
                return CreateCaptureObservable(videoSource, obPaused, scheduler);
            }
        }
    }
}