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
using System.Reactive.Linq;
using System.Reactive.Disposables;
using SimpleLPR3;

namespace VideoANPR.Observables
{
    // Represents the aggregated result of plate tracking
    public class AggregatedResultLPR
    {
        private readonly FrameResultLPR? frameResult_;
        private readonly IPlateCandidateTrackerResult? trackerResult_;

        public FrameResultLPR? FrameResult => frameResult_;
        public IPlateCandidateTrackerResult? TrackerResult => trackerResult_;

        public AggregatedResultLPR(FrameResultLPR? frameResult = null, IPlateCandidateTrackerResult? trackerResult = null)
        {
            frameResult_ = frameResult;
            trackerResult_ = trackerResult;
        }
    }

    public static class LicensePlateAggregateObservableExtension
    {
        /// <summary>
        /// Aggregates frame results using SimpleLPR's built-in plate candidate tracker.
        /// </summary>
        /// <param name="src">The source observable stream of frame results.</param>
        /// <param name="tracker">The SimpleLPR plate candidate tracker.</param>
        /// <returns>An observable stream of aggregated result objects.</returns>
        /// <remarks>
        /// NOTE: This operator doesn't dispose resources. The caller is responsible for disposing
        /// frames and tracker results using operators like .Do() at the end of the chain.
        /// </remarks>
        public static IObservable<AggregatedResultLPR> AggregateIntoRepresentatives(this IObservable<FrameResultLPR> src, IPlateCandidateTracker tracker)
        {
            return Observable.Create<AggregatedResultLPR>(o =>
            {
                bool bCompleted = false;

                void handleError(Exception ex)
                {
                    if (!bCompleted)
                    {
                        bCompleted = true;
                        o.OnError(ex);
                    }
                }

                var subscription = src.Subscribe(
                    // OnNext handler
                    frameResult =>
                    {
                        if (bCompleted) return;

                        try
                        {
                            // Always emit a result, but only process if the list of candidates is available
                            if (frameResult.Result.candidates != null)
                            {
                                // Process frame with tracker
                                var trackerResult = tracker.processFrameCandidates(frameResult.Result.candidates,frameResult.Frame);

                                // Emit with tracker result
                                o.OnNext(new AggregatedResultLPR(frameResult, trackerResult));
                            }
                            else
                            {
                                // No candidates to process - emit frame without tracker result
                                o.OnNext(new AggregatedResultLPR(frameResult));
                            }
                        }
                        catch (Exception ex)
                        {
                            handleError(ex);
                        }
                    },
                    // OnError handler
                    ex =>
                    {
                        if (!bCompleted)
                        {
                            // Flush any pending tracks
                            try
                            {
                                var flushResult = tracker.flush();
                                o.OnNext(new AggregatedResultLPR(null, flushResult));
                            }
                            catch { }

                            handleError(ex);
                        }
                    },
                    // OnCompleted handler
                    () =>
                    {
                        if (!bCompleted)
                        {
                            try
                            {
                                // Flush any pending tracks before completing
                                var flushResult = tracker.flush();
                                o.OnNext(new AggregatedResultLPR(null, flushResult));
                            }
                            catch (Exception ex)
                            {
                                handleError(ex);
                                return;
                            }

                            bCompleted = true;
                            o.OnCompleted();
                        }
                    }
                );

                return Disposable.Create(() =>
                {
                    bCompleted = true;
                    // Flush any pending tracks before disposing to reset the tracker to its initial state
                    var flushResult = tracker.flush();
                    flushResult.Dispose();
                    subscription.Dispose();
                });
            });
        }

        /// <summary>
        /// Alternative aggregation method that creates and manages its own tracker.
        /// </summary>
        /// <param name="src">The source observable stream of frame results.</param>
        /// <param name="lpr">The SimpleLPR instance to create the tracker.</param>
        /// <param name="trackerParams">The tracker configuration parameters.</param>
        /// <returns>An observable stream of aggregated result objects.</returns>
        public static IObservable<AggregatedResultLPR> AggregateIntoRepresentatives(
            this IObservable<FrameResultLPR> src,
            ISimpleLPR lpr,
            PlateCandidateTrackerSetupParms trackerParams)
        {
            return Observable.Using(
                () => lpr.createPlateCandidateTracker(trackerParams),
                tracker => src.AggregateIntoRepresentatives(tracker)
            );
        }
    }
}