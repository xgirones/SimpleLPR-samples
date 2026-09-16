package com.warelogic.simplelpr.samples;

import com.warelogic.simplelpr.EngineSetupParams;
import com.warelogic.simplelpr.FrameFormat;
import com.warelogic.simplelpr.PlateCandidateTracker;
import com.warelogic.simplelpr.PlateCandidateTrackerSetupParams;
import com.warelogic.simplelpr.PoolTrackingResult;
import com.warelogic.simplelpr.ProcessorPool;
import com.warelogic.simplelpr.SimpleLPR;
import com.warelogic.simplelpr.TrackedPlateCandidate;
import com.warelogic.simplelpr.TrackerResult;
import com.warelogic.simplelpr.VideoFrame;
import com.warelogic.simplelpr.VideoSource;
import com.warelogic.simplelpr.VideoSourceState;

import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * Tracks license plates across a video stream — the Java port of the .NET
 * {@code SimpleLPR_CSharp_Tracking} sample. Frames are analyzed concurrently by a
 * {@link ProcessorPool} and each result is fed, together with its frame, to a
 * {@link PlateCandidateTracker}; the tracker turns frame-by-frame detections into
 * one event per physical plate, with a representative thumbnail.
 *
 * <p>The instructive part is frame lifetime: a frame must stay open from its
 * launch until its result has been fed to the tracker (the tracker cuts the
 * thumbnail out of it), and results may arrive out of order — hence the pending
 * map from request id to frame, mirroring the .NET sample.</p>
 *
 * <pre>
 * usage: VideoTrackingSample --country &lt;code&gt; [--country &lt;code&gt; ...]
 *                            [--pool-size &lt;n&gt;] [--output &lt;folder&gt;]
 *                            [--native-path &lt;folder&gt;] [--product-key &lt;file&gt;]
 *                            &lt;uri&gt;
 *
 *   --output   Folder where plate thumbnails are saved as JPEG (optional).
 * </pre>
 */
public final class VideoTrackingSample {

    private static int trackCounter = 0;

    public static void main(String[] args) throws Exception {
        List<String> countries = new ArrayList<>();
        Path nativePath = null;
        Path productKey = null;
        Path outputFolder = null;
        String uri = null;
        int poolSize = Runtime.getRuntime().availableProcessors();

        for (int i = 0; i < args.length; ++i) {
            switch (args[i]) {
                case "--country":     countries.add(requireValue(args, ++i, "--country")); break;
                case "--pool-size":   poolSize = Integer.parseInt(requireValue(args, ++i, "--pool-size")); break;
                case "--output":      outputFolder = Paths.get(requireValue(args, ++i, "--output")); break;
                case "--native-path": nativePath = Paths.get(requireValue(args, ++i, "--native-path")); break;
                case "--product-key": productKey = Paths.get(requireValue(args, ++i, "--product-key")); break;
                case "--help": case "-h": printUsage(); return;
                default:
                    if (args[i].startsWith("--")) {
                        System.err.println("Unknown option: " + args[i]);
                        printUsage();
                        System.exit(2);
                    }
                    uri = args[i];
            }
        }

        if (uri == null || countries.isEmpty()) {
            printUsage();
            System.exit(2);
        }
        if (outputFolder != null) {
            Files.createDirectories(outputFolder);
        }

        try (SimpleLPR lpr = (nativePath != null) ? SimpleLPR.setup(new EngineSetupParams(), nativePath)
                                                  : SimpleLPR.setup(new EngineSetupParams())) {
            if (productKey != null) {
                lpr.setProductKey(productKey);
            } else {
                System.out.println("No product key supplied: running in evaluation mode.");
            }

            for (String code : lpr.getSupportedCountries()) {
                lpr.setCountryWeight(code, 0.0f);
            }
            for (String code : countries) {
                lpr.setCountryWeight(code, 1.0f);
            }
            lpr.realizeCountryWeights();

            PlateCandidateTrackerSetupParams trackerParams = new PlateCandidateTrackerSetupParams();
            System.out.printf(Locale.ROOT,
                              "Tracker: trigger window %.1fs, max idle %.1fs, min %d detections%n",
                              trackerParams.getTriggerWindowInSec(),
                              trackerParams.getMaxIdleTimeInSec(),
                              trackerParams.getMinTriggerFrameCount());

            // BGR24 so thumbnails come out in color; use GRAY8 when nothing is saved.
            try (VideoSource source = lpr.openVideoSource(uri, FrameFormat.BGR24, 1920, -1);
                 ProcessorPool pool = lpr.createProcessorPool(poolSize);
                 PlateCandidateTracker tracker = lpr.createPlateCandidateTracker(trackerParams)) {

                pool.setPlateRegionDetectionEnabled(true);
                pool.setCropToPlateRegionEnabled(true);

                // Frames stay open from launch until their result is fed to the
                // tracker. Results may arrive out of order: correlate by request id.
                Map<Long, VideoFrame> pendingFrames = new HashMap<>();
                long t0 = System.nanoTime();
                long framesRead = 0;

                for (VideoFrame frame; (frame = source.nextFrame()) != null; ) {
                    ++framesRead;
                    long requestId = frame.getSequenceNumber();
                    pendingFrames.put(requestId, frame);
                    pool.launchAnalyzeVideoFrame(0, requestId, ProcessorPool.TIMEOUT_INFINITE, frame, null);

                    // Drain whatever has already finished, without blocking.
                    for (PoolTrackingResult r;
                         (r = pool.pollNextTrackingResult(0, ProcessorPool.TIMEOUT_IMMEDIATE)) != null; ) {
                        handleResult(r, pendingFrames, tracker, outputFolder);
                    }
                }

                // Stream ended: wait for the remaining results...
                while (pool.getOngoingRequestCount(0) > 0) {
                    PoolTrackingResult r = pool.pollNextTrackingResult(0, ProcessorPool.TIMEOUT_INFINITE);
                    if (r != null) {
                        handleResult(r, pendingFrames, tracker, outputFolder);
                    }
                }

                // ...and close whatever the tracker was still following.
                try (TrackerResult flushed = tracker.flush()) {
                    reportTrackerResult(flushed, outputFolder);
                }

                double elapsedSec = (System.nanoTime() - t0) / 1_000_000_000.0;
                System.out.printf(Locale.ROOT,
                                  "%nStream ended in state %s: %d frame(s), %d track(s) in %.2f s (%.1f fps)%n",
                                  source.getState(), framesRead, trackCounter,
                                  elapsedSec, framesRead / Math.max(elapsedSec, 1e-9));

                if (source.getState() != VideoSourceState.EOF) {
                    System.err.println("Warning: the stream did not end normally.");
                }
            }
        } catch (Exception e) {
            System.err.println();
            System.err.println("ERROR: " + e.getMessage());
            System.exit(1);
        }
    }

    /**
     * Feeds one pool result (and its frame) to the tracker, then disposes both.
     * The try-with-resources closes the result and the frame in every path,
     * including per-request failures and exceptions — the lifetime discipline
     * this sample exists to demonstrate.
     */
    private static void handleResult(PoolTrackingResult result, Map<Long, VideoFrame> pendingFrames,
                                     PlateCandidateTracker tracker, Path outputFolder) {
        VideoFrame frame = pendingFrames.remove(result.getRequestId());
        try (PoolTrackingResult r = result; VideoFrame f = frame) {
            if (!r.isSuccess()) {
                System.out.printf("frame %5d  FAILED: %s%n", r.getRequestId(), r.getError().getMessage());
                return;
            }
            try (TrackerResult events = tracker.processFrameCandidates(r, f)) {
                reportTrackerResult(events, outputFolder);
            }
        }
    }

    /** Prints new/closed track events and saves thumbnails when an output folder is set. */
    private static void reportTrackerResult(TrackerResult events, Path outputFolder) {
        for (TrackedPlateCandidate track : events.getNewTracks()) {
            ++trackCounter;
            String text = track.getBestText();
            System.out.printf(Locale.ROOT,
                              "NEW  track %2d  %-12s frames %d..%d  t=%.2fs..%.2fs%n",
                              trackCounter, text != null ? text : "(unmatched)",
                              track.getFirstDetectionFrameId(), track.getNewestDetectionFrameId(),
                              track.getFirstDetectionTimestamp(), track.getNewestDetectionTimestamp());

            VideoFrame thumbnail = track.getRepresentativeThumbnail();
            if (outputFolder != null && thumbnail != null) {
                String safeText = text != null ? text.replaceAll("[^A-Za-z0-9]", "_") : "unmatched";
                Path jpeg = outputFolder.resolve(String.format("track_%03d_%s.jpg", trackCounter, safeText));
                thumbnail.saveAsJPEG(jpeg, 90);
                System.out.println("     thumbnail -> " + jpeg);
            }
        }
        for (TrackedPlateCandidate track : events.getClosedTracks()) {
            System.out.printf(Locale.ROOT,
                              "END  %-12s last seen frame %d (t=%.2fs)%n",
                              track.getBestText() != null ? track.getBestText() : "(unmatched)",
                              track.getNewestDetectionFrameId(), track.getNewestDetectionTimestamp());
        }
    }

    private static String requireValue(String[] args, int index, String option) {
        if (index >= args.length) {
            System.err.println("Missing value for " + option);
            printUsage();
            System.exit(2);
        }
        return args[index];
    }

    private static void printUsage() {
        System.out.println("usage: VideoTrackingSample --country <code> [--country <code> ...]");
        System.out.println("                           [--pool-size <n>] [--output <folder>]");
        System.out.println("                           [--native-path <folder>] [--product-key <file>] <uri>");
    }

    private VideoTrackingSample() {}
}
