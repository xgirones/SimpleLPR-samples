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
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive;
using System.Collections.Concurrent;
using System.Threading;
using System.Reactive.Linq;
using System.Reactive.Disposables;

namespace VideoANPR.Observables
{
    // Represents the result of processing a video frame with SimpleLPR.
    public class FrameResultLPR
    {
        private readonly Emgu.CV.Mat frame_;  // The frame
        private readonly int index_;          // The index of the frame
        private readonly TimeSpan timestamp_; // The timestamp of the frame
        private List<SimpleLPR3.Candidate>? candidates_;  // List of license plate candidates found in the frame

        public Emgu.CV.Mat Frame => frame_;
        public int Index => index_;
        public TimeSpan Timestamp => timestamp_;
        public List<SimpleLPR3.Candidate>? Candidates
        {
            get => candidates_;
            internal set => candidates_ = value;
        }

        public FrameResultLPR(Emgu.CV.Mat frame, int index, TimeSpan timestamp, List<SimpleLPR3.Candidate>? candidates = null)
        {
            frame_ = frame;
            index_ = index;
            timestamp_ = timestamp;
            candidates_ = candidates;
        }
    }

    public static class SimpleLPRObservableExtension
    {
        // Represents a work item in the processing pipeline
        private class WorkItem
        {            
            public SimpleLPR3.IProcessor? proc = null; // The LPR processor
            public FrameResultLPR? res = null;         // The frame result
            public bool bCompleted = false;            // Flag indicating if the processing is completed    
            public SharedContext? ctx = null;          // Reference to the shared context common to all work items
        }

        private class SharedContext
        {
            public readonly Queue<WorkItem> runningQ = new Queue<WorkItem>();   // Queue of running work items
            public IObserver<FrameResultLPR>? ob = null;                        // Observer for emitting frame results
            public BlockingCollection<SimpleLPR3.IProcessor>? pool = null;      // Thread-safe collection of LPR processors

            public volatile int idxLast = -1;                   // Last processed frame index
            public volatile int idxIgnoreAfter = int.MaxValue;  // Index after which frames should be ignored
            public volatile bool bDisposed = false;             // Flag indicating if the context is disposed
        }

        /// <summary>
        /// converts an observable sequence of video frames (`TimeInterval<Emgu.CV.Mat>`) into an observable
        /// sequence of LPR frame results. It utilizes a pool of LPR processors to process the frames asynchronously.
        /// </summary>
        /// <param name="src">The source observable of frames with timestamps.</param>
        /// <param name="pcs">The collection of LPR processors.</param>
        /// <param name="scheduler">The scheduler for controlling concurrency.</param>
        /// <param name="bExhaustive">Controls the processor acquisition behavior:
        /// When bExhaustive is set to true, it means that the method will exhaustively attempt to acquire a processor from the pool.
        /// It will call ctx.pool.Take(), which is a blocking operation. If no processors are currently available in the pool,
        /// the method will wait until a processor becomes available before proceeding. This ensures that a processor is always
        /// acquired for each frame, even if it means temporarily blocking the execution.
        /// When bExhaustive is set to false, the method will try to acquire a processor from the pool without blocking.
        /// It will call ctx.pool.TryTake(out proc), which attempts to remove a processor from the pool if one is available.
        /// If a processor is not immediately available, the method will proceed without acquiring a processor for that frame.
        /// This allows non-blocking execution and is suitable when there are more frames than available processors.
        /// </param>
        /// <returns>A transformed observable sequence of `FrameResultLPR` objects that can be subscribed to by an observer.</returns>
        public static IObservable<FrameResultLPR> ToSimpleLPR(
             this IObservable<TimeInterval<Emgu.CV.Mat>> src,
             IEnumerable<SimpleLPR3.IProcessor> pcs,
             IScheduler scheduler,
             bool bExhaustive = true)
        {
            var lprObservable = Observable.Create<FrameResultLPR>(o =>
            {
                SharedContext ctx = new SharedContext();  // Create the shared context for LPR processing
                ctx.pool = new BlockingCollection<SimpleLPR3.IProcessor>(new ConcurrentBag<SimpleLPR3.IProcessor>(pcs)); // Initialize the processor pool
                if (ctx.pool.Count == 0) { throw new ArgumentException($"{nameof(pcs)} is empty"); }  // Ensure the supplied processor pool is not empty
                ctx.ob = o; // Set the observer for emitting frame results

                // Iterates over the completed work items in the running queue and notifies observers
                // about the frame results. It repeatedly dequeues the last completed work item and checks if its result is valid
                // (not null) and its index is within the allowed range. If so, it notifies the observer by invoking OnNext()
                // with the frame result.
                void DispatchCompleted()
                {
                    for (;;)
                    {
                        WorkItem? wiLast = null;

                        // Acquire a lock on the running queue to ensure thread safety
                        lock (ctx.runningQ)
                        {
                            // Check if there are items in the running queue and if the first item is completed
                            if (ctx.runningQ.Count > 0 && ctx.runningQ.Peek().bCompleted)
                            {
                                // Dequeue the last completed work item from the running queue
                                wiLast = ctx.runningQ.Dequeue();
                            }
                        }

                        // If no completed work item is found, exit the loop
                        if (wiLast == null)
                            break;

                        // Check if the result of the last completed work item is valid (not null),
                        // its index is within the allowed range, and the context is not disposed
                        if (wiLast.res != null && wiLast.res.Index <= ctx.idxIgnoreAfter && !ctx.bDisposed)
                        {
                            // Notify the observer about the frame result by invoking OnNext() with the frame result
                            ctx.ob!.OnNext(wiLast!.res);
                        }
                    }
                }

                // Dispatches all pending items in the work queue
                void DispatchAllPending()
                {
                    for (;;)
                    {
                        lock (ctx.runningQ)
                        {
                            // Stop when there are no more pending items
                            if (ctx.runningQ.Count == 0)
                                break;
                        }

                        Thread.Sleep(50);    // Wait for a short duration
                        DispatchCompleted(); // Dispatch any finished work items.
                    }
                }

                var subscription = src.Subscribe(
                    // Element handler
                    tsf =>
                    {
                        SimpleLPR3.IProcessor? proc = null;

                        // Determine whether to exhaustively take a processor from the pool or try to take one
                        if (bExhaustive) { proc = ctx.pool.Take(); } else { ctx.pool.TryTake(out proc); }

                        // Increment the index of the last processed frame in a thread-safe manner
                        int idxCur = Interlocked.Increment(ref ctx.idxLast);

                        // Check if a processor is available and if the frame index is within the allowed range
                        if (proc != null && idxCur <= ctx.idxIgnoreAfter && !ctx.bDisposed)
                        {
                            // Create a new work item to encapsulate the processor and frame result
                            WorkItem wi = new WorkItem();                            
                            wi.proc = proc;
                            wi.res = new FrameResultLPR(tsf.Value, idxCur, tsf.Interval);
                            wi.bCompleted = false;
                            wi.ctx = ctx;

                            // Add the work item to the running queue
                            lock (ctx.runningQ) { ctx.runningQ.Enqueue(wi); }

                            // Initialize an exception variable to track any errors during processing
                            Exception? ex = null;

                            // Schedule the work item for processing on the provided scheduler
                            scheduler.Schedule(
                                wi,
                                (st, self) =>
                                {
                                    try
                                    {
                                        // Perform frame analysis using the SimpleLPR processor if not already disposed
                                        if (!ctx.bDisposed)
                                        {
                                            st.res!.Candidates = wi.proc.analyze_C3(st.res.Frame.DataPointer,
                                                                                    (uint)st.res.Frame.Step, (uint)st.res.Frame.Width, (uint)st.res.Frame.Height,
                                                                                    0.114f, 0.587f, 0.299f);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        // Handle any exceptions by capturing the error and adjusting the ignored frame ind
                                        ex = e;
                                        ctx.idxIgnoreAfter = st.res!.Index - 1;
                                    }
                                    finally
                                    {
                                        // Add the processor back to the pool and mark the work item as completed
                                        if (st.proc != null)
                                        {
                                            st.ctx!.pool!.Add(st.proc);
                                        }

                                        wi.bCompleted = true;
                                    }

                                    // Process any completed work items
                                    DispatchCompleted();

                                    // Handle any exceptions by dispatching pending work items, notifying observers, and propagating the error
                                    if (ex != null)
                                    {
                                        DispatchAllPending();

                                        if (!ctx.bDisposed)
                                        {
                                            ctx.ob.OnError(ex);
                                        }
                                    }
                                });
                        } //  if (proc != null)
                    },
                    // Exception handler
                    ex =>
                    {
                        // Adjust the ignored frame index and process any pending work items
                        ctx.idxIgnoreAfter = ctx.idxLast;
                        DispatchAllPending();

                        // Propagate the error to observers
                        if (!ctx.bDisposed )
                        {          
                            ctx.ob.OnError(ex);
                        }
                    },
                    // Completion handler
                    () =>
                    {
                        // Adjust the ignored frame index and process any pending work items
                        ctx.idxIgnoreAfter = ctx.idxLast;
                        DispatchAllPending();

                        // Notify observers of completion
                        if ( !ctx.bDisposed)
                        {
                            ctx.ob.OnCompleted();                           
                        }

                    }); //src.Subscribe

                // Create a composite disposable to manage subscriptions and resources
                CompositeDisposable disposables = new CompositeDisposable();

                // Add a disposable to handle resource cleanup and pending work items when disposed
                disposables.Add(
                    Disposable.Create( () =>
                        {
                            // Mark the context as disposed and adjust the ignored frame index
                            ctx.bDisposed = true;
                            ctx.idxIgnoreAfter = ctx.idxLast;

                            // Process any pending work items
                            DispatchAllPending();
                        }));

                // Add the subscription to the composite disposable
                disposables.Add(subscription);

                return disposables;
            });

            return lprObservable;
        }
    }
}

