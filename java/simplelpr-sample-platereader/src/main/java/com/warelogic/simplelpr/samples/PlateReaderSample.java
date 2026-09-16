package com.warelogic.simplelpr.samples;

import com.warelogic.simplelpr.Candidate;
import com.warelogic.simplelpr.CountryMatch;
import com.warelogic.simplelpr.Element;
import com.warelogic.simplelpr.EngineSetupParams;
import com.warelogic.simplelpr.Processor;
import com.warelogic.simplelpr.SimpleLPR;

import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;

/**
 * Analyzes one image and prints every license plate candidate found — the Java
 * twin of the core loop in the .NET {@code SimpleLPR_CSharp} sample (without the
 * processor pool, which a later sample demonstrates).
 *
 * <pre>
 * usage: PlateReaderSample --country &lt;code&gt; [--country &lt;code&gt; ...]
 *                          [--native-path &lt;folder&gt;] [--product-key &lt;file&gt;]
 *                          &lt;image&gt;
 *
 *   --country       Country configuration to enable, e.g. "Spain" or "Germany"
 *                   (run the engine info sample for the full list). May be
 *                   repeated to enable several.
 *   --native-path   Folder containing the SimpleLPR native library; when omitted
 *                   the automatic locator chain is used.
 *   --product-key   Product key file; when omitted the engine runs in
 *                   evaluation mode.
 * </pre>
 */
public final class PlateReaderSample {

    public static void main(String[] args) {
        List<String> countries = new ArrayList<>();
        Path nativePath = null;
        Path productKey = null;
        Path image = null;

        // Plain manual argument parsing, kept dependency-free on purpose.
        for (int i = 0; i < args.length; ++i) {
            switch (args[i]) {
                case "--country":     countries.add(requireValue(args, ++i, "--country")); break;
                case "--native-path": nativePath = Paths.get(requireValue(args, ++i, "--native-path")); break;
                case "--product-key": productKey = Paths.get(requireValue(args, ++i, "--product-key")); break;
                case "--help": case "-h": printUsage(); return;
                default:
                    if (args[i].startsWith("--")) {
                        System.err.println("Unknown option: " + args[i]);
                        printUsage();
                        System.exit(2);
                    }
                    image = Paths.get(args[i]);
            }
        }

        if (image == null || countries.isEmpty()) {
            printUsage();
            System.exit(2);
        }
        if (!Files.isRegularFile(image)) {
            System.err.println("Image file not found: " + image);
            System.exit(1);
        }

        EngineSetupParams params = new EngineSetupParams(); // CPU, recommended settings

        // Java note: the try-with-resources statement below is the analog of C#'s
        // 'using' — close() is guaranteed to run when the block exits, in reverse
        // declaration order (processor first, then engine).
        try (SimpleLPR lpr = (nativePath != null) ? SimpleLPR.setup(params, nativePath)
                                                  : SimpleLPR.setup(params)) {
            if (productKey != null) {
                lpr.setProductKey(productKey);
            } else {
                System.out.println("No product key supplied: running in evaluation mode.");
            }

            // Disable everything, then enable the requested countries — the same
            // configuration pattern used by the .NET samples.
            for (String code : lpr.getSupportedCountries()) {
                lpr.setCountryWeight(code, 0.0f);
            }
            for (String code : countries) {
                lpr.setCountryWeight(code, 1.0f);
            }
            lpr.realizeCountryWeights();

            try (Processor proc = lpr.createProcessor()) {
                proc.setPlateRegionDetectionEnabled(true);
                proc.setCropToPlateRegionEnabled(true);

                long t0 = System.nanoTime();
                List<Candidate> candidates = proc.analyzeFile(image);
                double elapsedMs = (System.nanoTime() - t0) / 1_000_000.0;

                System.out.printf("%n%s: %d candidate(s) in %.1f ms%n%n",
                                  image.getFileName(), candidates.size(), elapsedMs);

                int c = 0;
                for (Candidate candidate : candidates) {
                    System.out.printf("Candidate %d: box %s, region confidence %.3f, %s background%n",
                                      c++, candidate.getBoundingBox(),
                                      candidate.getPlateRegionDetectionConfidence(),
                                      candidate.isBrightBackground() ? "bright" : "dark");

                    for (CountryMatch match : candidate.getMatches()) {
                        System.out.printf("  %-12s %-6s conf %.3f  box %s%n",
                                          match.getText(), "[" + match.getCountryISO() + "]",
                                          match.getConfidence(), match.getBoundingBox());
                        for (Element e : match.getElements()) {
                            System.out.printf("      %s%n", e);
                        }
                    }
                    if (candidate.getMatches().isEmpty()) {
                        System.out.println("  (no country match)");
                    }
                }
            }
        } catch (Exception e) {
            System.err.println();
            System.err.println("ERROR: " + e.getMessage());
            System.exit(1);
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
        System.out.println("usage: PlateReaderSample --country <code> [--country <code> ...]");
        System.out.println("                         [--native-path <folder>] [--product-key <file>] <image>");
    }

    private PlateReaderSample() {}
}
