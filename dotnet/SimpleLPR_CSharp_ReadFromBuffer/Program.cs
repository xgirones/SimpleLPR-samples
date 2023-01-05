using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SimpleLPR3;

namespace SimpleLPR_CSharp_ReadFromBuffer
{
    class Program
    {
        static void dump_candidates(List<Candidate> cds)
        {
            if (cds.Count == 0)
            {
                Console.WriteLine("No license plate found");
            }
            else
            {
                Console.WriteLine("{0} license plate candidates found:", cds.Count);

                // Iterate over all candidates

                foreach (Candidate cd in cds)
                {
                    Console.WriteLine("***********");
                    Console.WriteLine("Light background: {0}, left: {1}, top: {2}, width: {3}, height: {4}", cd.brightBackground, cd.bbox.Left, cd.bbox.Top, cd.bbox.Width, cd.bbox.Height);
                    Console.WriteLine("Plate confidence: {0}. Plate vertices: {1}", cd.plateDetectionConfidence, string.Join(", ", cd.plateRegionVertices));
                    Console.WriteLine("Matches:");

                    // Iterate over all country matches

                    foreach (CountryMatch match in cd.matches)
                    {
                        Console.WriteLine("-----------");
                        Console.WriteLine("Text: {0}, country: {1}, ISO: {2}, confidence: {3}", match.text, match.country, match.countryISO, match.confidence);
                        Console.WriteLine("Elements:");

                        foreach (Element e in match.elements)
                        {
                            Console.WriteLine("   Glyph: {0}, confidence: {1}, left: {2}, top: {3}, width: {4}, height: {5}",
                                              e.glyph, e.confidence, e.bbox.Left, e.bbox.Top, e.bbox.Width, e.bbox.Height);
                        }
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            // This sample demonstrates the use of the 'analyze' methods that are fed a memory buffer, which are advised for video processing.

            // Create an instance of the SimpleLPR engine.
            EngineSetupParms setupP;
            setupP.cudaDeviceId = -1; // Select CPU
            setupP.enableImageProcessingWithGPU = false;
            setupP.enableClassificationWithGPU = false;
            setupP.maxConcurrentImageProcessingOps = 0;  // Use the default value.  

            ISimpleLPR lpr = SimpleLPR.Setup(setupP);

            // Output the version number
            VersionNumber ver = lpr.versionNumber;
            Console.WriteLine("SimpleLPR version {0}.{1}.{2}.{3}", ver.A, ver.B, ver.C, ver.D);

            try
            {
                bool bOk = (args.Length == 2 || args.Length == 3);
                if (bOk)
                {
                    string sFilePath = args[1];

                    // Configure country weights based on the selected country

                    uint countryId = uint.Parse(args[0]);

                    if (countryId >= lpr.numSupportedCountries)
                        throw new Exception("Invalid country id");

                    for (uint ui = 0; ui < lpr.numSupportedCountries; ++ui)
                        lpr.set_countryWeight(ui, 0.0f);

                    lpr.set_countryWeight(countryId, 1.0f);

                    lpr.realizeCountryWeights();

                    // Set the product key (if supplied)
                    if (args.Length == 3)
                        lpr.set_productKey(args[2]);

                    // Create a processor object
                    IProcessor proc = lpr.createProcessor();
                    proc.plateRegionDetectionEnabled = true;
                    proc.cropToPlateRegionEnabled = true;

                    // 1. Use the analyze version that takes the path to a file
                    List<Candidate> cds = proc.analyze(sFilePath);

                    Console.WriteLine("***********");
                    Console.WriteLine("Results of IProcessor.analyze(file)");
                    dump_candidates(cds);

                    // 2. Use the analyze version that takes a Stream object.
                    using (FileStream fs = File.OpenRead(sFilePath))
                    {
                        cds = proc.analyze(fs);
                    }

                    Console.WriteLine("***********");
                    Console.WriteLine("Results of IProcessor.analyze(stream)");
                    dump_candidates(cds);

                    // 3. Use one of the versions that take a buffer in memory in accordance with the bitmap pixel format.

                    Bitmap bm = (Bitmap)Bitmap.FromFile(sFilePath);

                    Rectangle r = new Rectangle(0, 0, bm.Width, bm.Height);
                    BitmapData bmd = null;

                    // Only PixelFormat.Format24bppRgb and PixelFormat.Format8bppIndexed are supported

                    switch (bm.PixelFormat)
                    {
                        case PixelFormat.Format32bppArgb:
                            {
                                Bitmap bmClone = new Bitmap(bm.Width, bm.Height, PixelFormat.Format24bppRgb);

                                using (Graphics g = Graphics.FromImage(bmClone))
                                {
                                    g.DrawImage(bm, r);
                                }

                                bm = bmClone;

                                // Convert to RGB to grayscale employing the standard NTSC CRT coefficients.
                                // In spite of the pixel format description, the internal layout in memory is (A)BGR.

                                bmd = bm.LockBits(r, ImageLockMode.ReadOnly, bm.PixelFormat);
                                cds = proc.analyze_C3(bmd.Scan0,
                                                       (uint)bmd.Stride,
                                                       (uint)bm.Width,
                                                       (uint)bm.Height,
                                                       0.114f, 0.587f, 0.299f); // GDI++ INTERNAL LAYOUT IS BGR!!!
                            }
                            break;

                        case PixelFormat.Format24bppRgb:
                            {
                                // Convert to RGB to grayscale employing the standard NTSC CRT coefficients.
                                // In spite of the pixel format description, the internal layout in memory is BGR.

                                bmd = bm.LockBits(r, ImageLockMode.ReadOnly, bm.PixelFormat);
                                cds = proc.analyze_C3(bmd.Scan0,
                                                       (uint)bmd.Stride,
                                                       (uint)bm.Width,
                                                       (uint)bm.Height,
                                                       0.114f, 0.587f, 0.299f); // GDI++ INTERNAL LAYOUT IS BGR!!!
                            }
                            break;
                        case PixelFormat.Format8bppIndexed:
                            {
                                // Employ the 8bpp indexed raster directly. This will fail miserably in case that the bitmap palette is
                                // not trivial e.g. 0 -> {0,0,0}, 1 -> {1,1,1}, .. , 255 -> {255,255,255}.

                                bmd = bm.LockBits(r, ImageLockMode.ReadOnly, bm.PixelFormat);
                                cds = proc.analyze(bmd.Scan0,
                                                    (uint)bmd.Stride,
                                                    (uint)bm.Width,
                                                    (uint)bm.Height);
                            }
                            break;
                        default:
                            throw new Exception(String.Format("Unsupported pixel format: {0}", bm.PixelFormat.ToString()));
                    }

                    bm.UnlockBits(bmd);

                    Console.WriteLine("***********");
                    Console.WriteLine("Results of IProcessor.analyze(buffer)");
                    dump_candidates(cds);
                }
                else
                {
                    Console.WriteLine("\nUsage:  {0} <country id> <source file> [product key]", Environment.GetCommandLineArgs()[0]);
                    Console.WriteLine("    Parameters:");
                    Console.WriteLine("     country id: Country identifier. The allowed values are");

                    for (uint ui = 0; ui < lpr.numSupportedCountries; ++ui)
                        Console.WriteLine("\t\t\t{0}\t: {1}", ui, lpr.get_countryCode(ui));

                    Console.WriteLine("     source file: file containing the image to be processed");
                    Console.WriteLine("     product key:  product key file. this value is optional, if not provided SimpleLPR will run in evaluation mode");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Exception occurred!: {0}", ex.Message);
            }
        }
    }
}
