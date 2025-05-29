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
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using SimpleLPR3;

namespace VideoANPR.Observables
{
    // Represents the result of processing a video frame with SimpleLPR.
    public class FrameResultLPR
    {
        private readonly IVideoFrame frame_;
        private readonly IProcessorPoolResult result_;

        public IVideoFrame Frame => frame_;
        public IProcessorPoolResult Result => result_;

        public FrameResultLPR(IVideoFrame frame, IProcessorPoolResult result)
        {
            frame_ = frame;
            result_ = result;
        }
    }

    public static class SimpleLPRObservableExtension
    {
        /// <summary>
        /// Converts an observable sequence of video frames into an observable sequence of LPR frame results.
        /// It utilizes a SimpleLPR processor pool to process the frames asynchronously.
        /// </summary>
        /// <param name="src">The source observable of video frames.</param>
        /// <param name="pool">The SimpleLPR processor pool for performing ANPR.</param>
        /// <param name="streamId">The stream ID for the processor pool operations.</param>
        /// <param name="bExhaustive">Controls the processor acquisition behavior:
        /// When true, waits for a processor to become available (blocking).
        /// When false, skips frames if no processor is immediately available (non-blocking).
        /// </param>
        /// <returns>A transformed observable sequence of FrameResultLPR objects that can be subscribed to by an observer.</returns>
        /// <remarks>
        /// IMPORTANT: This operator NEVER disposes frames. The video source may have multiple subscribers,
        /// so frames must flow through all pipelines. Only the final consumer should dispose frames.
        /// This operator only owns and disposes IProcessorPoolResult objects when they cannot be delivered downstream.
        /// </remarks>
        public static IObservable<FrameResultLPR> ToSimpleLPR(
             this IObservable<IVideoFrame> src,
             IProcessorPool pool,
             int streamId = 0,
             bool bExhaustive = true)
        {
            // Determine timeout based on exhaustive parameter
            int launchTimeout = bExhaustive ? IProcessorPoolConstants.TIMEOUT_INFINITE : IProcessorPoolConstants.TIMEOUT_IMMEDIATE;

            return Observable.Create<FrameResultLPR>(o =>
            {
                // State variables (no locking needed due to Rx serialization guarantees)
                bool bCompleted = false;
                Queue<IVideoFrame> frameQ = new Queue<IVideoFrame>();

                void handleError(Exception ex)
                {
                    if (!bCompleted)
                    {
                        bCompleted = true;
                        o.OnError(ex);
                        discardPendingResults();
                    }
                }

                void discardPendingResults()
                {
                    frameQ.Clear();

                    while (pool.get_ongoingRequestCount(streamId) > 0)
                    {
                        IProcessorPoolResult result = pool.pollNextResult(streamId, IProcessorPoolConstants.TIMEOUT_INFINITE);
                        result?.Dispose();
                    }
                }

                void processResults(int pollTimeout)
                {
                    if (bCompleted) return;

                    try
                    {
                        IProcessorPoolResult result;
                        while (pool.get_ongoingRequestCount(streamId) > 0 && (result = pool.pollNextResult(streamId, pollTimeout)) != null)
                        {
                            // Should never be empty, but check to be safe
                            if (frameQ.Count > 0)
                            {
                                IVideoFrame frame = frameQ.Dequeue();

                                if (result.errorInfo != null)
                                {
                                    result.Dispose();
                                    throw result.errorInfo;
                                }

                                o.OnNext(new FrameResultLPR(frame, result));
                            }
                            else
                            {
                                // This shouldn't happen, but handle gracefully
                                result.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        handleError(ex);
                    }
                }

                var subscription = src.Subscribe(
                    // Element handler
                    frame =>
                    {
                        if (bCompleted) return;

                        processResults(IProcessorPoolConstants.TIMEOUT_IMMEDIATE);

                        try
                        {
                            if (pool.launchAnalyze(streamId, frame.sequenceNumber, launchTimeout, frame))
                            {
                                frameQ.Enqueue(frame);
                            }
                        }
                        catch (Exception ex)
                        {
                            handleError(ex);
                            return;
                        }

                        processResults(IProcessorPoolConstants.TIMEOUT_IMMEDIATE);
                    },
                    // Exception handler
                    ex =>
                    {
                        if (!bCompleted)
                        {
                            processResults(IProcessorPoolConstants.TIMEOUT_INFINITE);
                            if (!bCompleted) handleError(ex);
                        }
                    },
                    // Completion handler
                    () =>
                    {
                        if (!bCompleted)
                        {
                            processResults(IProcessorPoolConstants.TIMEOUT_INFINITE);

                            if (!bCompleted)
                            {
                                bCompleted = true;
                                o.OnCompleted();                               
                            }
                        }
                    }); //src.Subscribe

                // Return a disposable that cleans up
                return Disposable.Create(() =>
                {
                    if (!bCompleted)
                    {
                        bCompleted = true;
                        subscription.Dispose();
                        discardPendingResults();
                    }
                });
            });
        }
    }
}