/*
    SimpleLPR_CSharp

	Sample C# application demonstrating the usage of SimpleLPR
    Looks for license plates in all pictures in a folder an saves
    the output in a XML file.
 
	(c) Copyright Warelogic, 2009
	All rights reserved. Copying or other reproduction of this 
	program except for archival purposes is prohibited without
	written consent of Warelogic.
*/

using System;
using System.Collections.Generic;
using System.Xml;

using SimpleLPR3;

namespace SimpleLPR_CSharp
{
    struct Result
    {
        public string filePath;
        public List<Candidate> lps;
    }

    class Program
    {
        private ISimpleLPR _lpr;                // Instance of the SimpleLPR engine
        private List<Result> _results;          // Recognition results
        private Stack<IProcessor> _processors;  // Pool of IProcessor, to avoid creating and destroying IProcessor on each detection
        private int _cPending;                  // Number of pending operations

        // Analyze a picture asynchronously.
        private void process(Object threadContext)
        {
            string imgFileName = (string)threadContext;  // The image file to be processed is supplied as the asynchronous call context

            // Get an IProcessor from the processor pool
            System.Threading.Monitor.Enter(this);
            IProcessor proc = _processors.Pop();
            System.Threading.Monitor.Exit(this);

            List<Candidate> cds = proc.analyze(imgFileName); // Look for license plates

            System.Threading.Monitor.Enter(this);

            Result res;
            res.filePath = imgFileName;
            res.lps = cds;
            _results.Add(res); // Keep result

            _processors.Push(proc); // Return processor to the pool

            --_cPending;       // Decrement number of pending operations
            System.Threading.Monitor.Pulse(this);   // Signal end of operation
            System.Threading.Monitor.Exit(this);

            // Write result to the console

            System.IO.StringWriter strw = new System.IO.StringWriter();

            System.IO.FileInfo ffo = new System.IO.FileInfo(imgFileName);
            strw.Write("{0} : ", ffo.Name);

            if (cds.Count == 0)
                strw.WriteLine("Nothing detected.");
            else
            {
                foreach (Candidate cd in cds)
                {
                    // The last element in the 'matches' list always corresponds to the raw text.
                    // Therefore, single element lists correspond to unmatched candidates.

                    CountryMatch cm = cd.matches[0];

                    if (cd.matches.Count > 1)
                    {
                        strw.Write("[{0} --> {1}] ", cm.text, cm.confidence);
                    }
                    else
                    {
                        strw.Write("[{0} --> {1} (U)] ", cm.text, cm.confidence);
                    }
                }

                strw.WriteLine();
            }

            Console.Write("{0}", strw.ToString());
        }

        void doIt(bool bCPU, uint countryId, string srcFolder, string targetFile, string productKey)
        {
            _results.Clear();

            // Configure country weights based on the selected country

            if (countryId >= _lpr.numSupportedCountries)
                throw new Exception("Invalid country id");

            for (uint ui = 0; ui < _lpr.numSupportedCountries; ++ui)
                _lpr.set_countryWeight(ui, 0.0f);

            _lpr.set_countryWeight(countryId, 1.0f);

            _lpr.realizeCountryWeights();

            // Set the product key (if any)
            if (productKey != null)
                _lpr.set_productKey(productKey);

            // Initialize the pool of IProcessor

            int cLogicalCoresPerIProcessor = (bCPU ? 8 : 2);
            int cIProcessor = (Environment.ProcessorCount + cLogicalCoresPerIProcessor - 1) / cLogicalCoresPerIProcessor;

            _processors.Clear();
            for (int i = 0; i < cIProcessor; ++i)
            {
                IProcessor proc = _lpr.createProcessor();
                proc.plateRegionDetectionEnabled = true;
                proc.cropToPlateRegionEnabled = true;

                _processors.Push(proc);
            }

            // For each image in the source folder ... and sub folders
            System.IO.DirectoryInfo dInfo = new System.IO.DirectoryInfo(srcFolder);
            foreach (System.IO.FileInfo f in dInfo.GetFiles("*.*", System.IO.SearchOption.AllDirectories))
            {
                // Filter out non image files
                string ext = f.Extension.ToLower();

                if (ext == ".jpg" || ext == ".tif" ||
                     ext == ".png" || ext == ".bmp")
                {
                    System.Threading.Monitor.Enter(this);
                    while (_cPending >= cIProcessor) // Do not exceed cIProcessor of simultaneous operations
                        System.Threading.Monitor.Wait(this);

                    ++_cPending;
                    // Execute plate recognition as an asynchronous operation through the process method
                    System.Threading.ThreadPool.QueueUserWorkItem(process, f.FullName);

                    System.Threading.Monitor.Exit(this);
                }
            }

            // Wait for all operations to complete
            System.Threading.Monitor.Enter(this);
            while (_cPending > 0)
                System.Threading.Monitor.Wait(this);
            System.Threading.Monitor.Exit(this);

            // Sort results by file name.
            _results.Sort(delegate (Result r1, Result r2) { return (r1.filePath.CompareTo(r2.filePath)); });

            // Write out the results to a XML file
            XmlDocument xml = new XmlDocument();
            XmlProcessingInstruction basePI = xml.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\"");
            xml.AppendChild(basePI);
            XmlElement baseElem = xml.CreateElement("results");
            xml.AppendChild(baseElem);

            foreach (Result res in _results)
            {
                XmlElement xmlRes = xml.CreateElement("result");
                baseElem.AppendChild(xmlRes);

                XmlElement xmlFile = xml.CreateElement("file");
                xmlRes.AppendChild(xmlFile);
                xmlFile.SetAttribute("path", res.filePath);

                XmlElement xmlCds = xml.CreateElement("candidates");
                xmlRes.AppendChild(xmlCds);

                foreach (Candidate cand in res.lps)
                {
                    XmlElement xmlCd = xml.CreateElement("candidate");
                    xmlCds.AppendChild(xmlCd);

                    xmlCd.SetAttribute("lightBackground", cand.brightBackground.ToString());
                    xmlCd.SetAttribute("bbLeft", cand.bbox.Left.ToString());
                    xmlCd.SetAttribute("bbTop", cand.bbox.Top.ToString());
                    xmlCd.SetAttribute("bbWidth", cand.bbox.Width.ToString());
                    xmlCd.SetAttribute("bbHeight", cand.bbox.Height.ToString());
                    xmlCd.SetAttribute("plateDetConf", cand.plateDetectionConfidence.ToString());
                    xmlCd.SetAttribute("plateVertices", string.Join(", ", cand.plateRegionVertices));

                    XmlElement xmlMatches = xml.CreateElement("matches");
                    xmlCd.AppendChild(xmlMatches);

                    foreach (CountryMatch match in cand.matches)
                    {
                        XmlElement xmlMatch = xml.CreateElement("match");
                        xmlMatches.AppendChild(xmlMatch);

                        xmlMatch.SetAttribute("text", match.text);
                        xmlMatch.SetAttribute("country", match.country);
                        xmlMatch.SetAttribute("ISO", match.countryISO);
                        xmlMatch.SetAttribute("confidence", match.confidence.ToString());

                        XmlElement xmlElements = xml.CreateElement("elements");
                        xmlMatch.AppendChild(xmlElements);

                        foreach (Element elem in match.elements)
                        {
                            XmlElement xmlElem = xml.CreateElement("element");
                            xmlElements.AppendChild(xmlElem);

                            xmlElem.SetAttribute("glyph", elem.glyph.ToString());
                            xmlElem.SetAttribute("confidence", elem.confidence.ToString());
                            xmlElem.SetAttribute("bbLeft", elem.bbox.Left.ToString());
                            xmlElem.SetAttribute("bbTop", elem.bbox.Top.ToString());
                            xmlElem.SetAttribute("bbWidth", elem.bbox.Width.ToString());
                            xmlElem.SetAttribute("bbHeight", elem.bbox.Height.ToString());
                        }
                    }
                }
            }

            xml.Save(targetFile);
        }

        Program(ISimpleLPR lpr)
        {
            _lpr = lpr;
            _processors = new Stack<IProcessor>();
            _results = new List<Result>();
        }

        static void Main(string[] args)
        {
            try
            {
                bool bOk = (args.Length == 4 || args.Length == 5);
                if (bOk)
                {
                    // Create an instance of the SimpleLPR engine.
                    EngineSetupParms setupP;
                    setupP.cudaDeviceId = int.Parse(args[0]);
                    setupP.enableImageProcessingWithGPU = true;  // Only effective if the value provided in 'cudaDeviceId' is valid.
                    setupP.enableClassificationWithGPU = true;   // Only effective if the value provided in 'cudaDeviceId' is valid.
                    setupP.maxConcurrentImageProcessingOps = 0;  // Use the default value.  

                    ISimpleLPR lpr = SimpleLPR.Setup(setupP);

                    // Output version number
                    VersionNumber ver = lpr.versionNumber;
                    Console.WriteLine("SimpleLPR version {0}.{1}.{2}.{3}", ver.A, ver.B, ver.C, ver.D);

                    // Main program logic.
                    Program prg = new Program(lpr);
                    prg.doIt((setupP.cudaDeviceId) == -1, uint.Parse(args[1]), args[2], args[3], args.Length == 5 ? args[4] : null);
                }
                else
                {
                    // Create an instance of the SimpleLPR engine.
                    EngineSetupParms setupP;
                    setupP.cudaDeviceId = -1; // Select CPU
                    setupP.enableImageProcessingWithGPU = false;
                    setupP.enableClassificationWithGPU = false;
                    setupP.maxConcurrentImageProcessingOps = 0;  // Use the default value.  


                    ISimpleLPR lpr = SimpleLPR.Setup(setupP);

                    // Output version number
                    VersionNumber ver = lpr.versionNumber;
                    Console.WriteLine("SimpleLPR version {0}.{1}.{2}.{3}", ver.A, ver.B, ver.C, ver.D);

                    // Show usage.
                    Console.WriteLine("\nUsage:  {0} <CUDA device id> <country id> <source folder> <target file> [product key]", Environment.GetCommandLineArgs()[0]);
                    Console.WriteLine("    Parameters:");
                    Console.WriteLine("     CUDA device id: Valid CUDA device numeric identifier, or -1 for CPU");
                    Console.WriteLine("     Country id: Country identifier. The allowed values are");

                    for (uint ui = 0; ui < lpr.numSupportedCountries; ++ui)
                        Console.WriteLine("\t\t\t{0}\t: {1}", ui, lpr.get_countryCode(ui));

                    Console.WriteLine("     Source folder: Folder containing the images to be processed");
                    Console.WriteLine("     Target file:   File where the output will be written");

                    Console.WriteLine("     product key:  Product key file. this value is optional, if not provided SimpleLPR will run in evaluation mode");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Exception occurred!: {0}", ex.Message);
            }
        }
    }
}
