package com.warelogic.simplelpr.samples;

import com.warelogic.simplelpr.Candidate;
import com.warelogic.simplelpr.CountryMatch;
import com.warelogic.simplelpr.EngineSetupParams;
import com.warelogic.simplelpr.PoolResult;
import com.warelogic.simplelpr.ProcessorPool;
import com.warelogic.simplelpr.SimpleLPR;

import java.io.IOException;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Analyzes every image in a folder through a {@link ProcessorPool} — the Java
 * twin of the .NET {@code SimpleLPR_CSharp} sample: launch each file with an
 * infinite timeout, drain finished results without blocking after every launch,
 * then wait for the stragglers once everything is submitted.
 *
 * <pre>
 * usage: BatchFolderSample --country &lt;code&gt; [--country &lt;code&gt; ...]
 *                          [--pool-size &lt;n&gt;] [--native-path &lt;folder&gt;]
 *                          [--product-key &lt;file&gt;] &lt;folder&gt;
 *
 *   --pool-size     Number of concurrent processors; defaults to the CPU core count.
 * </pre>
 */
public final class BatchFolderSample {

    public static void main(String[] args) {
        List<String> countries = new ArrayList<>();
        Path nativePath = null;
        Path productKey = null;
        Path folder = null;
        int poolSize = Runtime.getRuntime().availableProcessors();

        for (int i = 0; i < args.length; ++i) {
            switch (args[i]) {
                case "--country":     countries.add(requireValue(args, ++i, "--country")); break;
                case "--pool-size":   poolSize = Integer.parseInt(requireValue(args, ++i, "--pool-size")); break;
                case "--native-path": nativePath = Paths.get(requireValue(args, ++i, "--native-path")); break;
                case "--product-key": productKey = Paths.get(requireValue(args, ++i, "--product-key")); break;
                case "--help": case "-h": printUsage(); return;
                default:
                    if (args[i].startsWith("--")) {
                        System.err.println("Unknown option: " + args[i]);
                        printUsage();
                        System.exit(2);
                    }
                    folder = Paths.get(args[i]);
            }
        }

        if (folder == null || countries.isEmpty()) {
            printUsage();
            System.exit(2);
        }

        List<Path> images = listImages(folder);
        if (images.isEmpty()) {
            System.err.println("No images (jpg/jpeg/png/tif/bmp) found in " + folder);
            System.exit(1);
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

            System.out.printf("Analyzing %d image(s) with a pool of %d processor(s)...%n%n",
                              images.size(), poolSize);

            try (ProcessorPool pool = lpr.createProcessorPool(poolSize)) {
                pool.setPlateRegionDetectionEnabled(true);
                pool.setCropToPlateRegionEnabled(true);

                long t0 = System.nanoTime();
                int done = 0;

                // Launch every file; the request id doubles as the index into
                // 'images', which is how results are correlated back to files.
                for (int i = 0; i < images.size(); ++i) {
                    pool.launchAnalyzeFile(0, i, 0.0, ProcessorPool.TIMEOUT_INFINITE, images.get(i));

                    // Drain whatever has already finished, without blocking.
                    for (PoolResult r; (r = pool.pollNextResult(0, ProcessorPool.TIMEOUT_IMMEDIATE)) != null; ) {
                        printResult(images, r);
                        ++done;
                    }
                }

                // Everything submitted; wait for the remaining results.
                while (pool.getOngoingRequestCount(0) > 0) {
                    PoolResult r = pool.pollNextResult(0, ProcessorPool.TIMEOUT_INFINITE);
                    if (r != null) {
                        printResult(images, r);
                        ++done;
                    }
                }

                double elapsedSec = (System.nanoTime() - t0) / 1_000_000_000.0;
                System.out.printf(Locale.ROOT,
                                  "%n%d image(s) in %.2f s  (%.1f images/s)%n",
                                  done, elapsedSec, done / elapsedSec);
            }
        } catch (Exception e) {
            System.err.println();
            System.err.println("ERROR: " + e.getMessage());
            System.exit(1);
        }
    }

    /** Prints one result line: file name plus the best match of each candidate. */
    private static void printResult(List<Path> images, PoolResult r) {
        String name = images.get((int) r.getRequestId()).getFileName().toString();

        if (!r.isSuccess()) {
            System.out.printf("%-28s FAILED: %s%n", name, r.getError().getMessage());
            return;
        }

        StringBuilder sb = new StringBuilder();
        for (Candidate c : r.getCandidates()) {
            // Matches are ordered best-first; show the top one per candidate.
            if (!c.getMatches().isEmpty()) {
                CountryMatch best = c.getMatches().get(0);
                if (sb.length() > 0) sb.append("  |  ");
                sb.append(best.getText())
                  .append(" [").append(best.getCountryISO()).append(']')
                  .append(String.format(Locale.ROOT, " %.3f", best.getConfidence()));
            }
        }
        if (sb.length() == 0) {
            sb.append("(no plate found)");
        }
        System.out.printf("%-28s %s%n", name, sb);
    }

    private static List<Path> listImages(Path folder) {
        List<Path> result = new ArrayList<>();
        try (DirectoryStream<Path> dir = Files.newDirectoryStream(folder)) {
            for (Path p : dir) {
                String n = p.getFileName().toString().toLowerCase(Locale.ROOT);
                if (n.endsWith(".jpg") || n.endsWith(".jpeg") || n.endsWith(".png")
                        || n.endsWith(".tif") || n.endsWith(".bmp")) {
                    result.add(p);
                }
            }
        } catch (IOException e) {
            System.err.println("Cannot read folder " + folder + ": " + e.getMessage());
            System.exit(1);
        }
        result.sort(null); // deterministic order (alphabetical)
        return result;
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
        System.out.println("usage: BatchFolderSample --country <code> [--country <code> ...]");
        System.out.println("                         [--pool-size <n>] [--native-path <folder>]");
        System.out.println("                         [--product-key <file>] <folder>");
    }

    private BatchFolderSample() {}
}
