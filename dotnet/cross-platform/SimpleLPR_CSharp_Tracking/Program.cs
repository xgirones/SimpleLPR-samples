/*
    SimpleLPR_CSharp_Tracking

    Sample C# console application demonstrating the usage of SimpleLPR's
    license plate tracking functionality. Processes video sources (files or streams)
    and tracks license plates across frames, saving results to XML files and thumbnails.
 
    Usage: SimpleLPR_CSharp_Tracking <video_source> <country_id> <output_folder> [product_key]

    (c) Copyright Warelogic
    All rights reserved. Copying or other reproduction of this
    program except for archival purposes is prohibited without
    written consent of Warelogic.
*/

using System.Xml;
using SimpleLPR3;

namespace SimpleLPR_CSharp_Tracking
{
    class Program
    {
        private static ISimpleLPR? _lpr;
        private static IProcessorPool? _pool;
        private static IPlateCandidateTracker? _tracker;
        private static IVideoSource? _videoSource;
        private static volatile bool _shouldStop = false;
        private static int _trackCounter = 0;

        static void Main(string[] args)
        {
            // Set up Ctrl+C handling
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _shouldStop = true;
                Console.WriteLine("\nStopping gracefully... Please wait.");
            };

            try
            {
                if (args.Length < 3 || args.Length > 4)
                {
                    ShowUsage();
                    return;
                }

                string videoSource = args[0];
                uint countryId = uint.Parse(args[1]);
                string outputFolder = args[2];
                string? productKey = args.Length == 4 ? args[3] : null;

                // Ensure output folder exists
                Directory.CreateDirectory(outputFolder);

                // Initialize SimpleLPR
                InitializeEngine(countryId, productKey);

                // Process video
                ProcessVideo(videoSource, outputFolder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                // Clean up resources
                CleanupResources();
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("SimpleLPR License Plate Tracking Demo");
            Console.WriteLine("\nUsage: SimpleLPR_CSharp_Tracking <video_source> <country_id> <output_folder> [product_key]");
            Console.WriteLine("\nParameters:");
            Console.WriteLine("  video_source  : Path to video file or stream URL (e.g., rtsp://...)");
            Console.WriteLine("  country_id    : Country identifier (see list below)");
            Console.WriteLine("  output_folder : Directory where results will be saved");
            Console.WriteLine("  product_key   : Optional product key file path");

            // Create temporary engine instance to show supported countries
            var setupParams = new EngineSetupParms
            {
                cudaDeviceId = -1,
                enableImageProcessingWithGPU = false,
                enableClassificationWithGPU = false,
                maxConcurrentImageProcessingOps = 1
            };

            using (var tempLpr = SimpleLPR.Setup(setupParams))
            {
                Console.WriteLine("\nSupported countries:");
                for (uint i = 0; i < tempLpr.numSupportedCountries; i++)
                {
                    Console.WriteLine($"  {i,3} : {tempLpr.get_countryCode(i)}");
                }
            }
        }

        static void InitializeEngine(uint countryId, string? productKey)
        {
            Console.WriteLine("Initializing SimpleLPR engine...");

            // Setup engine parameters (CPU only)
            var setupParams = new EngineSetupParms
            {
                cudaDeviceId = -1,                    // Use CPU
                enableImageProcessingWithGPU = false,
                enableClassificationWithGPU = false,
                maxConcurrentImageProcessingOps = 0   // Default concurrent operations for CPU
            };

            _lpr = SimpleLPR.Setup(setupParams);

            // Display version
            var version = _lpr.versionNumber;
            Console.WriteLine($"SimpleLPR version: {version.A}.{version.B}.{version.C}.{version.D}");

            // Validate country ID
            if (countryId >= _lpr.numSupportedCountries)
            {
                throw new ArgumentException($"Invalid country ID. Must be between 0 and {_lpr.numSupportedCountries - 1}");
            }

            // Configure country weights
            for (uint i = 0; i < _lpr.numSupportedCountries; i++)
            {
                _lpr.set_countryWeight(i, 0.0f);
            }
            _lpr.set_countryWeight(countryId, 1.0f);
            _lpr.realizeCountryWeights();

            Console.WriteLine($"Selected country: {_lpr.get_countryCode(countryId)}");

            // Set product key if provided
            if (!string.IsNullOrEmpty(productKey))
            {
                if (!File.Exists(productKey))
                {
                    throw new FileNotFoundException($"Product key file not found: {productKey}");
                }
                _lpr.set_productKey(productKey);
                Console.WriteLine("Product key loaded successfully");
            }
            else
            {
                Console.WriteLine("Running in evaluation mode");
            }

            // Create processor pool
            _pool = _lpr!.createProcessorPool();
            _pool.plateRegionDetectionEnabled = true;
            _pool.cropToPlateRegionEnabled = false;

            // Create tracker with default parameters
            var trackerParams = PlateCandidateTrackerSetupParms.Default;
            _tracker = _lpr.createPlateCandidateTracker(trackerParams);

            Console.WriteLine($"\nTracker configuration:");
            Console.WriteLine($"  Trigger window: {trackerParams.triggerWindowInSec}s");
            Console.WriteLine($"  Max idle time:  {trackerParams.maxIdleTimeInSec}s");
            Console.WriteLine($"  Min frames:     {trackerParams.minTriggerFrameCount}");
        }

        static void ProcessVideo(string videoSourcePath, string outputFolder)
        {
            Console.WriteLine($"\nOpening video source: {videoSourcePath}");

            // Open video source with reasonable frame size limits
            _videoSource = _lpr!.openVideoSource(videoSourcePath,
                                              FrameFormat.FRAME_FORMAT_BGR24,
                                              1920,  // Max width
                                              1080); // Max height

            if (_videoSource.state != VideoSourceState.VIDEO_SOURCE_STATE_OPEN)
            {
                throw new Exception($"Failed to open video source. State: {_videoSource.state}");
            }

            Console.WriteLine($"Video source type: {(_videoSource.isLiveSource ? "Live stream" : "File")}");
            Console.WriteLine("\nProcessing frames... Press Ctrl+C to stop.");

            // Process frames
            var frameQueue = new Queue<IVideoFrame>();
            var frameCount = 0;
            var lastProgressUpdate = DateTime.Now;
            var progressMessage = "";

            try
            {
                IVideoFrame frame;
                while (!_shouldStop && (frame = _videoSource.nextFrame()) != null)
                {
                    frameCount++;
                    frameQueue.Enqueue(frame);

                    // Launch analysis (will throw on error with TIMEOUT_INFINITE)
                    _pool!.launchAnalyze(
                        streamId: 0,
                        requestId: frame.sequenceNumber,
                        timeoutInMs: IProcessorPoolConstants.TIMEOUT_INFINITE,
                        frame);

                    // Poll for results
                    ProcessPendingResults(frameQueue, outputFolder, progressMessage);

                    // Update progress periodically
                    if ((DateTime.Now - lastProgressUpdate).TotalSeconds > 0.5)
                    {
                        progressMessage = $"Processing: {frameCount,5} frames, {_trackCounter,3} tracks detected";
                        Console.Write($"\r{progressMessage}");
                        lastProgressUpdate = DateTime.Now;
                    }
                }

                // Process remaining results
                Console.WriteLine("\n\nProcessing remaining frames...");
                while (_pool!.get_ongoingRequestCount(0) > 0 && !_shouldStop)
                {
                    var result = _pool.pollNextResult(0, 100); // 100ms timeout for responsiveness
                    if (result != null)
                    {
                        ProcessResult(result, frameQueue, outputFolder);
                    }
                }

                // Flush tracker to get any remaining tracks
                if (!_shouldStop)
                {
                    Console.WriteLine("Flushing tracker...");
                    var flushResult = _tracker!.flush();
                    ProcessTrackerResult(flushResult, -1, outputFolder);
                    flushResult.Dispose();
                }

                // Clear progress line before printing completion message
                Console.Write("\r" + new string(' ', 60) + "\r");
            }
            finally
            {
                // Clean up remaining frames
                while (frameQueue.Count > 0)
                {
                    frameQueue.Dequeue().Dispose();
                }
            }

            Console.WriteLine($"\n\nProcessing complete!");
            Console.WriteLine($"Total frames processed: {frameCount,5}");
            Console.WriteLine($"Total tracks detected:  {_trackCounter,5}");
            Console.WriteLine($"Results saved to: {outputFolder}");
        }

        static void ProcessPendingResults(Queue<IVideoFrame> frameQueue, string outputFolder, string? progressMessage = null)
        {
            IProcessorPoolResult result;
            while ((result = _pool!.pollNextResult(0, IProcessorPoolConstants.TIMEOUT_IMMEDIATE)) != null)
            {
                ProcessResult(result, frameQueue, outputFolder, progressMessage);
            }
        }

        static void ProcessResult(IProcessorPoolResult result, Queue<IVideoFrame> frameQueue, string outputFolder, string? progressMessage = null)
        {
            var frame = frameQueue.Dequeue();

            try
            {
                if (result.errorInfo != null)
                {
                    Console.WriteLine($"\nError processing frame {frame.sequenceNumber}: {result.errorInfo.Message}");
                    return;
                }

                if (result.candidates == null || result.candidates.Count == 0)
                {
                    // No candidates in this frame
                    return;
                }

                // Process frame with tracker
                var trackerResult = _tracker!.processFrameCandidates(result.candidates, frame);
                ProcessTrackerResult(trackerResult, frame.timestamp, outputFolder, progressMessage);
                trackerResult.Dispose();
            }
            finally
            {
                frame.Dispose();
                result.Dispose();
            }
        }

        static void ProcessTrackerResult(IPlateCandidateTrackerResult trackerResult, double timestamp, string outputFolder, string? progressMessage = null)
        {
            // Clear the progress line if there's any output to show
            bool hasOutput = trackerResult.NewTracks.Count > 0 || trackerResult.ClosedTracks.Count > 0;
            if (hasOutput && !string.IsNullOrEmpty(progressMessage))
            {
                Console.Write("\r" + new string(' ', progressMessage.Length) + "\r"); // Clear the line
            }

            // Process new tracks
            foreach (var track in trackerResult.NewTracks)
            {
                var candidate = track.representativeCandidate;
                if (candidate.matches.Count > 0)
                {
                    var match = candidate.matches[0];
                    var isMatched = !string.IsNullOrEmpty(match.countryISO);

                    Console.WriteLine($"[NEW] Frame {track.firstDetectionFrameId,6} @ {track.firstDetectionTimestamp,6:F2}s: " +
                                    $"{match.text,-12} {(isMatched ? $"({match.country})" : "(unmatched)")}");
                }
            }

            // Process closed tracks
            foreach (var track in trackerResult.ClosedTracks)
            {
                _trackCounter++;
                SaveTrackData(track, outputFolder, timestamp);
            }

            // Restore progress message if we had output
            if (hasOutput && !string.IsNullOrEmpty(progressMessage))
            {
                Console.Write($"\r{progressMessage}");
            }
        }

        static void SaveTrackData(ITrackedPlateCandidate track, string outputFolder, double currentTimestamp)
        {
            var candidate = track.representativeCandidate;
            if (candidate.matches.Count == 0)
            {
                Console.WriteLine("\nWarning: Track with no matches encountered");
                return;
            }

            var bestMatch = candidate.matches[0];
            var isMatched = !string.IsNullOrEmpty(bestMatch.countryISO);

            // Create safe filename
            var safeText = SanitizeFilename(bestMatch.text);
            var frameId = track.representativeFrameId;
            var timestamp = track.representativeTimestamp;

            // Log track closure
            var duration = track.newestDetectionTimestamp - track.firstDetectionTimestamp;
            var frameRange = track.newestDetectionFrameId - track.firstDetectionFrameId + 1;

            Console.WriteLine($"[CLOSED] {bestMatch.text,-12} " +
                            $"{(isMatched ? $"({bestMatch.country})" : "(unmatched)"),-15} " +
                            $"Duration: {duration,4:F2}s, Frames: {frameRange,3} " +
                            $"[{track.firstDetectionFrameId}-{track.newestDetectionFrameId}]");

            // Save thumbnail if available
            string? thumbnailPath = null;
            if (track.representativeThumbnail != null)
            {
                thumbnailPath = Path.Combine(outputFolder, $"track_{frameId:D6}_{timestamp:F2}_{safeText}.jpg");
                track.representativeThumbnail.saveAsJPEG(thumbnailPath, 95);
            }

            // Save XML data
            var xmlPath = Path.Combine(outputFolder, $"track_{frameId:D6}_{timestamp:F2}_{safeText}.xml");
            SaveTrackXml(track, candidate, xmlPath, thumbnailPath);
        }

        static void SaveTrackXml(ITrackedPlateCandidate track, Candidate candidate, string xmlPath, string? thumbnailPath)
        {
            var xml = new XmlDocument();
            var declaration = xml.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\"");
            xml.AppendChild(declaration);

            var root = xml.CreateElement("track");
            xml.AppendChild(root);

            // Track metadata
            var metadata = xml.CreateElement("metadata");
            root.AppendChild(metadata);

            AddXmlElement(xml, metadata, "firstDetectionFrameId", track.firstDetectionFrameId.ToString());
            AddXmlElement(xml, metadata, "firstDetectionTimestamp", track.firstDetectionTimestamp.ToString("F3"));
            AddXmlElement(xml, metadata, "newestDetectionFrameId", track.newestDetectionFrameId.ToString());
            AddXmlElement(xml, metadata, "newestDetectionTimestamp", track.newestDetectionTimestamp.ToString("F3"));
            AddXmlElement(xml, metadata, "representativeFrameId", track.representativeFrameId.ToString());
            AddXmlElement(xml, metadata, "representativeTimestamp", track.representativeTimestamp.ToString("F3"));
            AddXmlElement(xml, metadata, "duration",
                         (track.newestDetectionTimestamp - track.firstDetectionTimestamp).ToString("F3"));

            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                AddXmlElement(xml, metadata, "thumbnailPath", Path.GetFileName(thumbnailPath));
            }

            // Representative candidate data
            var candidateElem = xml.CreateElement("representativeCandidate");
            root.AppendChild(candidateElem);

            candidateElem.SetAttribute("brightBackground", candidate.brightBackground.ToString());
            candidateElem.SetAttribute("plateDetectionConfidence", candidate.plateDetectionConfidence.ToString("F3"));

            // Bounding box
            var bbox = xml.CreateElement("boundingBox");
            candidateElem.AppendChild(bbox);
            bbox.SetAttribute("left", candidate.bbox.Left.ToString());
            bbox.SetAttribute("top", candidate.bbox.Top.ToString());
            bbox.SetAttribute("width", candidate.bbox.Width.ToString());
            bbox.SetAttribute("height", candidate.bbox.Height.ToString());

            // Plate region vertices
            if (candidate.plateRegionVertices != null && candidate.plateRegionVertices.Length > 0)
            {
                var vertices = xml.CreateElement("plateRegionVertices");
                candidateElem.AppendChild(vertices);

                for (int i = 0; i < candidate.plateRegionVertices.Length; i++)
                {
                    var vertex = xml.CreateElement("vertex");
                    vertices.AppendChild(vertex);
                    vertex.SetAttribute("index", i.ToString());
                    vertex.SetAttribute("x", candidate.plateRegionVertices[i].X.ToString());
                    vertex.SetAttribute("y", candidate.plateRegionVertices[i].Y.ToString());
                }
            }

            // Matches
            var matches = xml.CreateElement("matches");
            candidateElem.AppendChild(matches);

            foreach (var match in candidate.matches)
            {
                var matchElem = xml.CreateElement("match");
                matches.AppendChild(matchElem);

                matchElem.SetAttribute("text", match.text);
                matchElem.SetAttribute("country", match.country ?? "");
                matchElem.SetAttribute("countryISO", match.countryISO ?? "");
                matchElem.SetAttribute("confidence", match.confidence.ToString("F3"));
                matchElem.SetAttribute("isRawText", string.IsNullOrEmpty(match.countryISO).ToString());

                // Elements (characters)
                var elements = xml.CreateElement("elements");
                matchElem.AppendChild(elements);

                foreach (var elem in match.elements)
                {
                    var elemNode = xml.CreateElement("element");
                    elements.AppendChild(elemNode);

                    elemNode.SetAttribute("glyph", elem.glyph.ToString());
                    elemNode.SetAttribute("confidence", elem.confidence.ToString("F3"));
                    elemNode.SetAttribute("left", elem.bbox.Left.ToString());
                    elemNode.SetAttribute("top", elem.bbox.Top.ToString());
                    elemNode.SetAttribute("width", elem.bbox.Width.ToString());
                    elemNode.SetAttribute("height", elem.bbox.Height.ToString());
                }
            }

            xml.Save(xmlPath);
        }

        static void AddXmlElement(XmlDocument doc, XmlElement parent, string name, string value)
        {
            var elem = doc.CreateElement(name);
            elem.InnerText = value;
            parent.AppendChild(elem);
        }

        static string SanitizeFilename(string filename)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", filename.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }

        static void CleanupResources()
        {
            Console.WriteLine("\nCleaning up resources...");

            _tracker?.Dispose();
            _pool?.Dispose();
            _videoSource?.Dispose();
            _lpr?.Dispose();
        }
    }
}