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
    class WorkItem
    {
        public string filePath;
        public IProcessorPoolResult result;
    }

    class Program
    {
        private ISimpleLPR _lpr;                // Instance of the SimpleLPR engine
        private List<WorkItem> _workItems;      // Scheduled detections
        private IProcessorPool _pool;           // Pool of IProcessor, to avoid creating and destroying IProcessor on each detection

        // Write result to the console
        private void PrintResult(WorkItem wi)
        {
            System.IO.StringWriter strw = new System.IO.StringWriter();

            System.IO.FileInfo ffo = new System.IO.FileInfo(wi.filePath);
            strw.Write("{0} : ", ffo.Name);

            if (wi.result.errorInfo != null)
            {
                strw.WriteLine("Exception occurred: {0}", wi.result.errorInfo.ToString());
            }
            else
            {
                if (wi.result.candidates.Count == 0)
                {
                    strw.WriteLine("Nothing detected.");
                }
                else
                {
                    foreach (Candidate cd in wi.result.candidates)
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
            }

            Console.Write("{0}", strw.ToString());
        }

        void ProcessImages(bool bCPU, uint countryId, string srcFolder, string targetFile, string productKey)
        {
            _workItems.Clear();

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

            // Initialize the pool of IProcessor with a default number of processors
            _pool = _lpr.createProcessorPool();
            _pool.plateRegionDetectionEnabled = true;
            _pool.cropToPlateRegionEnabled = true;

            // For each image in the source folder ... and sub folders
            System.IO.DirectoryInfo dInfo = new System.IO.DirectoryInfo(srcFolder);
            foreach (System.IO.FileInfo f in dInfo.GetFiles("*.*", System.IO.SearchOption.AllDirectories))
            {
                // Filter out non image files
                string ext = f.Extension.ToLower();

                if (ext == ".jpg" || ext == ".tif" ||
                     ext == ".png" || ext == ".bmp")
                {
                    _workItems.Add(new WorkItem { filePath = f.FullName });
                    _pool.launchAnalyze(streamId: 0,
                                        requestId: (uint)_workItems.Count - 1,
                                        timestampInSec: 0.0,
                                        timeoutInMs: IProcessorPoolConstants.TIMEOUT_INFINITE,
                                        f.FullName);
                    for (IProcessorPoolResult result = _pool.pollNextResult(streamId: 0, timeoutInMs: IProcessorPoolConstants.TIMEOUT_IMMEDIATE);
                         result != null;
                         result = _pool.pollNextResult(streamId: 0, timeoutInMs: IProcessorPoolConstants.TIMEOUT_IMMEDIATE))
                    {
                        WorkItem wi = _workItems[(int)result.requestId];
                        wi.result = result;
                        PrintResult(wi);
                    }
                }
            }

            // Process any remaining operations
            while (_pool.get_ongoingRequestCount(0) > 0)
            {
                IProcessorPoolResult result = _pool.pollNextResult(streamId: 0, timeoutInMs: IProcessorPoolConstants.TIMEOUT_INFINITE);
                WorkItem wi = _workItems[(int)result.requestId];
                wi.result = result;
                PrintResult(wi);
            }

            // Sort results by file name.
            _workItems.Sort(delegate (WorkItem wi1, WorkItem wi2) { return (wi1.filePath.CompareTo(wi2.filePath)); });

            // And write them out to a XML file
            XmlDocument xml = new XmlDocument();
            XmlProcessingInstruction basePI = xml.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\"");
            xml.AppendChild(basePI);
            XmlElement baseElem = xml.CreateElement("results");
            xml.AppendChild(baseElem);

            foreach (WorkItem wi in _workItems)
            {
                XmlElement xmlRes = xml.CreateElement("result");
                baseElem.AppendChild(xmlRes);

                XmlElement xmlFile = xml.CreateElement("file");
                xmlRes.AppendChild(xmlFile);
                xmlFile.SetAttribute("path", wi.filePath);

                XmlElement xmlCds = xml.CreateElement("candidates");
                xmlRes.AppendChild(xmlCds);

                foreach (Candidate cand in wi.result.candidates)
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
            _workItems = new List<WorkItem>();
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
                    prg.ProcessImages((setupP.cudaDeviceId) == -1, uint.Parse(args[1]), args[2], args[3], args.Length == 5 ? args[4] : null);
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