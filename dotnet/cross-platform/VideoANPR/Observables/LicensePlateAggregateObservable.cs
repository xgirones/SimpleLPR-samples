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
using System.Linq;
using System.Reactive.Linq;
using F23.StringSimilarity;

namespace VideoANPR.Observables
{
    public class AggregatedResultLPR
    {
        private readonly SimpleLPR3.Candidate cand_;
        private readonly Emgu.CV.Mat frame_;
        private readonly int index_;
        private readonly TimeSpan timestamp_;

        public SimpleLPR3.Candidate Candidate => cand_;
        public Emgu.CV.Mat Frame => frame_;
        public int Index => index_;
        public TimeSpan Timestamp => timestamp_;

        /// <summary>
        /// Initializes a new instance of the AggregatedResultLPR class.
        /// </summary>
        /// <param name="cand">The candidate associated with the aggregated result.</param>
        /// <param name="frame">The frame associated with the aggregated result.</param>
        /// <param name="index">The index associated with the aggregated result.</param>
        /// <param name="timestamp">The timestamp associated with the aggregated result.</param>
        public AggregatedResultLPR(SimpleLPR3.Candidate cand,
                                   Emgu.CV.Mat frame,
                                   int index,
                                   TimeSpan timestamp)
        {
            cand_ = cand;
            frame_ = frame;
            index_ = index;
            timestamp_ = timestamp;
        }
    }

    public static class LicensePlateAggregateObservableExtension
    {
        // Class to track candidates and aggregate results
        private class Tracker
        {            
            private class CandidateTracker
            {
                // Class to track information for each candidate
                public Dictionary<string, uint> hits = new Dictionary<string, uint>(); // Stores the hit counts for different candidate strings
                public bool bNotified = false;                 // Flag to indicate if the candidate has been notified
                public uint cBestHitsWeightSoFar = 0;          // Weighted hit count for the best candidate
                public AggregatedResultLPR? bestSoFar = null;  // Best aggregated result so far for the candidate
                public TimeSpan timeFirstHit;                  // Timestamp of the first hit
                public TimeSpan timeLatestHit;                 // Timestamp of the latest hit
            }

            private List<CandidateTracker> tracked_;   // List of candidate trackers

            internal Tracker()
            {
                tracked_ = new List<CandidateTracker>();
            }

            /// <summary>
            /// Calculates the best similarity between a candidate string and the hits in the candidate tracker.
            /// </summary>
            /// <param name="jw">The Jaro-Winkler similarity calculator.</param>
            /// <param name="ct">The candidate tracker.</param>
            /// <param name="s">The candidate string to compare.</param>
            /// <param name="fMinStringSimilarity">The minimum required string similarity.</param>
            /// <param name="fMinGroupMatchRatio">The minimum required group match ratio.</param>
            /// <returns>The best similarity score.</returns>
            private double BestSimilarity(JaroWinkler jw, CandidateTracker ct, string s, double fMinStringSimilarity, double fMinGroupMatchRatio)
            {
                double fBestSim = 0;  // The best similarity score found so far
                uint cCompatible = 0; // Number of compatible matches found

                // Iterate over the tracked hits for the candidate
                foreach (string k in ct.hits.Keys)
                {
                    double fSim = jw.Similarity(k, s); // Calculate the similarity between two candidates using the Jaro-Winkler algorithm
                    if (fSim > fMinStringSimilarity)   // Check if the similarity score exceeds the minimum required threshold
                    {
                        ++cCompatible;  // Increment the count of compatible matches

                        if (fSim > fBestSim)
                        {
                            fBestSim = fSim;  // Update the best similarity score if a higher score is found
                        }                       
                    }
                }

                // Check if the number of compatible matches is below the required group match ratio
                if (cCompatible < (double)ct.hits.Count * fMinGroupMatchRatio)
                {
                    fBestSim = 0;  // Reset the best similarity score to 0
                }

                return fBestSim;
            }

            /// <summary>
            /// Calculates the best similarity between all the matches in a candidate and the hits in the candidate tracker.
            /// </summary>
            /// <param name="jw">The JaroWinkler similarity calculator.</param>
            /// <param name="ct">The candidate tracker.</param>
            /// <param name="cand">The candidate to compare.</param>
            /// <param name="fMinStringSimilarity">The minimum required string similarity.</param>
            /// <param name="fMinGroupMatchRatio">The minimum required group match ratio.</param>
            /// <returns>The best similarity score.</returns>
            private double BestSimilarity(JaroWinkler jw, CandidateTracker ct, SimpleLPR3.Candidate cand, double fMinStringSimilarity, double fMinGroupMatchRatio)
            {
                // Use LINQ to calculate the best similarity score among all the matches in the candidate
                return (from m in cand.matches select BestSimilarity(jw, ct, m.text.Replace(" ", ""), fMinStringSimilarity, fMinGroupMatchRatio)).Max();
            }

            /// <summary>
            /// Handles a new candidate detection using a provided candidate tracker. It tracks the best candidate based on hit counts and weighted hits counts,
            /// and updates the ct.bestSoFar result with the most favorable candidate, considering various conditions and triggering thresholds
            /// </summary>
            /// <param name="triggerWindow">The trigger window duration.</param>
            /// <param name="maxIdleTime">The maximum idle time for a candidate.</param>
            /// <param name="cMinTriggerFrameCount">The minimum required trigger frame count.</param>
            /// <param name="resLPR">The frame result for the candidate.</param>
            /// <param name="cand">The candidate.</param>
            /// <param name="ct">The candidate tracker.</param>
            private void OnNewCandidate(TimeSpan triggerWindow,
                                        TimeSpan maxIdleTime,
                                        uint cMinTriggerFrameCount,
                                        FrameResultLPR resLPR,
                                        SimpleLPR3.Candidate cand,
                                        CandidateTracker ct)
            {
                ct.timeLatestHit = resLPR.Timestamp;

                // Check that the candidate tracker has not been notified yet
                if (!ct.bNotified)
                {
                    string sBestRawPlateText = string.Empty;
                    uint cBestHits = 0;
                    uint cBestHitsWeight = 0;

                    List<string> matchStrings = new List<string>();

                    // Iterate through each match in the candidate's matches collection
                    foreach (SimpleLPR3.CountryMatch m in cand.matches)
                    {
                        string sRawPlateText = m.text.Replace(" ", ""); // Remove spaces

                        // Ensure that each string is only considered once.
                        if (!matchStrings.Contains(sRawPlateText))
                        {
                            matchStrings.Add(sRawPlateText);

                            uint cHits;
                            bool bHit = ct.hits.TryGetValue(sRawPlateText, out cHits);

                            // Update the hit count for the specific plate text
                            if (!bHit)
                            {
                                cHits = 1;
                                ct.hits.Add(sRawPlateText, cHits);
                            }
                            else
                            {
                                ct.hits[sRawPlateText] = ++cHits;
                            }

                            uint cHitsWeight = cHits * (uint)sRawPlateText.Length;

                            // Update the best raw plate text and hit counts if a better match is found
                            if (cHitsWeight > cBestHitsWeight)
                            {
                                sBestRawPlateText = sRawPlateText;
                                cBestHits = cHits;
                                cBestHitsWeight = cHitsWeight;
                            }
                        }
                    }

                    // Check if the candidate meets the triggering conditions for reporting
                    if (cBestHits >= cMinTriggerFrameCount &&
                        (ct.bestSoFar is null ||
                          cand.matches.Count > 1 &&
                            (ct.bestSoFar.Candidate.matches.Count <= 1 || ct.bestSoFar.Candidate.matches.Count > 1 && cBestHitsWeight > ct.cBestHitsWeightSoFar) ||
                          cand.matches.Count <= 1 &&
                            ct.bestSoFar.Candidate.matches.Count <= 1 && cBestHitsWeight > ct.cBestHitsWeightSoFar))
                    {
                        // Dispose of the previous best frame if it exists
                        if (ct.bestSoFar != null) ct.bestSoFar.Frame.Dispose();

                        // Update the bestSoFar result with the new candidate information
                        ct.cBestHitsWeightSoFar = cBestHitsWeight;
                        ct.bestSoFar = new AggregatedResultLPR(cand, resLPR.Frame.Clone(), resLPR.Index, resLPR.Timestamp);
                    }
                }
            }

            /// <summary>
            /// Handles a new candidate detection. It performs a comparison between the new candidate and the existing tracked candidates,
            /// selects the most suitable candidate tracker based on similarity scores, and delegates the new candidate handling to the selected tracker.
            /// </summary>
            /// <param name="triggerWindow">The trigger window duration.</param>
            /// <param name="maxIdleTime">The maximum idle time for a candidate.</param>
            /// <param name="cMinTriggerFrameCount">The minimum required trigger frame count.</param>
            /// <param name="fMinStringSimilarity">The minimum required string similarity.</param>
            /// <param name="fMinGroupMatchRatio">The minimum required group match ratio.</param>
            /// <param name="resLPR">The frame result for the candidate.</param>
            /// <param name="cand">The candidate.</param>
            private void OnNewCandidate(TimeSpan triggerWindow,
                                        TimeSpan maxIdleTime,
                                        uint cMinTriggerFrameCount,
                                        double fMinStringSimilarity,
                                        double fMinGroupMatchRatio,
                                        FrameResultLPR resLPR,
                                        SimpleLPR3.Candidate cand)
            {
                double fBestSim = -1.0;
                CandidateTracker? ct = null;
                
                JaroWinkler jw = new JaroWinkler();

                // Iterate through each tracked candidate
                foreach (CandidateTracker c in tracked_)
                {
                    // Calculate the best similarity between the current tracked candidate and the new candidate
                    double fSim = BestSimilarity(jw, c, cand, fMinStringSimilarity, fMinGroupMatchRatio);

                    // Check if the similarity meets the minimum similarity threshold and is better than the previous best similarity
                    if (fSim >= fMinStringSimilarity && fSim > fBestSim)
                    {
                        fBestSim = fSim;
                        ct = c;
                    }
                }
                
                // If no suitable candidate tracker is found, create a new one and add it to the tracked list
                if (ct is null)
                {
                    ct = new CandidateTracker();
                    ct.timeFirstHit = resLPR.Timestamp;
                    tracked_.Add(ct);
                }

                // Call the overloaded OnNewCandidate method with the selected candidate tracker
                OnNewCandidate(triggerWindow, maxIdleTime, cMinTriggerFrameCount, resLPR, cand, ct);
            }

            /// <summary>
            /// This method is invoked when a new frame is received for processing. It handles the frame by processing the candidates,
            /// reporting suitable candidates to an observer, pruning stale candidates, and disposing of the input frame if necessary.
            /// </summary>
            /// <param name="o">The observer to notify with the aggregated results.</param>
            /// <param name="triggerWindow">The trigger window duration.</param>
            /// <param name="maxIdleTime">The maximum idle time for a candidate.</param>
            /// <param name="disposeInputFrames">Specifies whether to dispose the input frames after processing.</param>
            /// <param name="discardNonLPCandidates">Specifies whether to discard non-LP candidates.</param>
            /// <param name="cMinTriggerFrameCount">The minimum required trigger frame count.</param>
            /// <param name="fMinStringSimilarity">The minimum required string similarity.</param>
            /// <param name="fMinGroupMatchRatio">The minimum required group match ratio.</param>
            /// <param name="resLPR">The frame result.</param>
            internal void OnNewFrame(IObserver<AggregatedResultLPR> o,
                                     TimeSpan triggerWindow,
                                     TimeSpan maxIdleTime,
                                     bool disposeInputFrames,
                                     bool discardNonLPCandidates,
                                     uint cMinTriggerFrameCount,
                                     double fMinStringSimilarity,
                                     double fMinGroupMatchRatio,
                                     FrameResultLPR resLPR)
            {
                // Check if the Candidates collection is null
                if (resLPR.Candidates is null) throw new ArgumentNullException($"{nameof(resLPR.Candidates)} is null");

                // Process each candidate in the Candidates collection
                foreach (SimpleLPR3.Candidate cand in resLPR.Candidates)
                {
                    // Check if non-license plate candidates should be discarded or if the current candidate has a plate detection confidence greater than 0
                    if (!discardNonLPCandidates || cand.plateDetectionConfidence > 0)
                    {
                        // Call OnNewCandidate to handle the new candidate
                        OnNewCandidate(triggerWindow, maxIdleTime, cMinTriggerFrameCount, fMinStringSimilarity, fMinGroupMatchRatio, resLPR, cand);
                    }
                }

                // Report all suitable candidates, starting with the oldest.
                foreach (CandidateTracker ct in tracked_)
                {
                    // Check if the candidate tracker hasn't been notified yet, has the bestSoFar candidate, and has exceeded the triggerWindow or maxIdleTime
                    if (!ct.bNotified &&
                         ct.bestSoFar != null &&
                           ((resLPR.Timestamp - ct.timeFirstHit) >= triggerWindow || (resLPR.Timestamp - ct.timeLatestHit) > maxIdleTime))
                    {
                        // Notify the observer with the bestSoFar candidate
                        o.OnNext(ct.bestSoFar);
                        ct.bNotified = true;
                    }
                }

                // Prune stale candidates from the tracked list based on maxIdleTime
                tracked_.RemoveAll(ct => (resLPR.Timestamp - ct.timeLatestHit) > maxIdleTime);

                // Dispose the input frame if disposeInputFrames is true
                if (disposeInputFrames)
                {
                    resLPR.Frame.Dispose();
                }
            }

            /// <summary>
            /// Notifies the observer of pending candidates.
            /// </summary>
            /// <param name="o">The observer to be notified.</param>
            internal void NotifyPending(IObserver<AggregatedResultLPR> o)
            {
                // Report all pending candidates, starting with the oldest.
                foreach (CandidateTracker ct in tracked_)
                {
                    // Check if the candidate tracker hasn't been notified and has a valid bestSoFar candidate
                    if (!ct.bNotified && ct.bestSoFar != null )
                    {
                        // Notify the observer with the bestSoFar candidate
                        o.OnNext(ct.bestSoFar);
                        ct.bNotified = true;
                    }
                }
            }
        }

        /// <summary>
        /// Aggregates frame results from an observable stream into representative results based
        /// on the supplied parameters. It groups consecutive frames within a specified time window
        /// and considers them part of the same group. The method applies similarity and match ratio
        /// thresholds to identify the best candidates within each group, producing aggregated result
        /// objects. The purpose is to condense and extract meaningful information from the stream of
        /// frame results, facilitating further analysis or processing of license plate recognition data.
        /// </summary>
        /// <param name="src">The source observable stream of frame results.</param>
        /// <param name="triggerWindow">The time window within which consecutive frames are considered part of the same group.</param>
        /// <param name="maxIdleTime">The maximum idle time allowed between frames in a group.</param>
        /// <param name="disposeInputFrames">Specifies whether to dispose of input frames after processing.</param>
        /// <param name="discardNonLPCandidates">Specifies whether to discard candidates with low license plate detection confidence.</param>
        /// <param name="cMinTriggerFrameCount">The minimum number of frames required to trigger a result.</param>
        /// <param name="fMinStringSimilarity">The minimum similarity threshold for comparing license plate strings.</param>
        /// <param name="fMinGroupMatchRatio">The minimum ratio of matching candidates required within a group.</param>
        /// <returns>An observable stream of aggregated result objects.</returns>
        public static IObservable<AggregatedResultLPR> AggregateIntoRepresentatives(
            this IObservable<FrameResultLPR> src,
            TimeSpan triggerWindow,
            TimeSpan maxIdleTime,
            bool disposeInputFrames = false,
            bool discardNonLPCandidates = false,
            uint cMinTriggerFrameCount = 3,
            double fMinStringSimilarity = 0.75,
            double fMinGroupMatchRatio = 0.333)
        {
            return Observable.Create<AggregatedResultLPR>(o =>
            {
                Tracker tracker = new Tracker();

                return src.Subscribe(
                    // Element handler
                    fr => { lock (tracker) { tracker.OnNewFrame(o,
                                                                triggerWindow,
                                                                maxIdleTime,
                                                                disposeInputFrames,
                                                                discardNonLPCandidates,
                                                                cMinTriggerFrameCount,
                                                                fMinStringSimilarity,
                                                                fMinGroupMatchRatio,
                                                                fr); } },
                    // Exception handler
                    ex =>
                    {
                        lock (tracker) { tracker.NotifyPending(o); }
                        o.OnError(ex);
                    },
                    // Completion handler
                    () =>
                    {
                        lock (tracker) { tracker.NotifyPending(o); }
                        o.OnCompleted();
                    });
            });
        }
    }
}
