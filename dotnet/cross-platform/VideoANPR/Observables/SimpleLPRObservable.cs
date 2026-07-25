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
        /// OWNERSHIP MODEL (read this before reusing the operator):
        /// SimpleLPR frames wrap native memory, so SOMEBODY must dispose every frame exactly
        /// once -- relying on garbage collection alone strands megabytes of native memory per
        /// frame until finalizers eventually run. The convention used throughout this demo is
        /// "single consumer owns the frame":
        ///   - Frames successfully paired with an analysis result are handed downstream
        ///     inside a FrameResultLPR; the FINAL subscriber disposes them (see the OnNext
        ///     handler in MainWindowViewModel).
        ///   - Frames that can never reach the subscriber -- declined launches, and frames
        ///     still queued when the pipeline fails or shuts down -- are disposed HERE,
        ///     because no one else will ever see them.
        /// If you fan the same frame stream out to MULTIPLE processing pipelines, this
        /// convention no longer works: neither pipeline can know when the other is finished
        /// with a frame. In that case remove the disposals below and introduce reference
        /// counting around each frame, or give each pipeline its own video source.
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
                    // These frames were queued while waiting for their analysis results, but
                    // the pipeline is shutting down: no result will ever be paired with them
                    // and no subscriber will ever receive them. Under the single-consumer
                    // ownership model (see the remarks above) they must be disposed here --
                    // merely clearing the queue, as earlier versions did, stranded their
                    // native buffers until the garbage collector's finalizers ran.
                    while (frameQ.Count > 0)
                    {
                        frameQ.Dequeue().Dispose();
                    }

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

                                // The 'errorInfo' property queries the native result on
                                // EVERY access -- it is not cached. Fetch it into a local
                                // BEFORE disposing the result: the previous code disposed
                                // first and then read the property again in the throw
                                // statement, which raised a NullReferenceException instead
                                // of the real analysis error.
                                Exception? analysisError = result.errorInfo;
                                if (analysisError != null)
                                {
                                    // Neither the result nor the frame will reach the
                                    // subscriber; dispose both before propagating the error.
                                    result.Dispose();
                                    frame.Dispose();
                                    throw analysisError;
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
                            else
                            {
                                // Non-exhaustive mode only: no processor became available in
                                // time, so this frame is skipped. It will never be paired
                                // with a result nor delivered downstream, so it is disposed
                                // here (single-consumer ownership; see the remarks above).
                                frame.Dispose();
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

