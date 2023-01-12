Imports System

Module Program

    Sub Main()

        ' Create an instance of the SimpleLPR engine.

        Dim lpr As SimpleLPR3.ISimpleLPR

        Dim setupP As SimpleLPR3.EngineSetupParms
        setupP.cudaDeviceId = -1 ' Use CPU.
        setupP.enableImageProcessingWithGPU = False
        setupP.enableClassificationWithGPU = False
        setupP.maxConcurrentImageProcessingOps = 0 ' Use the Default value.  
        lpr = SimpleLPR3.SimpleLPR.Setup(setupP)

        ' Display the version number.

        Dim ver As SimpleLPR3.VersionNumber
        ver = lpr.versionNumber

        Console.WriteLine("SimpleLPR version {0}.{1}.{2}.{3}", ver.A, ver.B, ver.C, ver.D)

        Dim args As String()
        args = Environment.GetCommandLineArgs()

        If args.Count >= 3 And args.Count <= 4 Then

            'Configure the country weights based on the selected country.

            Dim countryId As UInteger
            countryId = UInteger.Parse(args(1))

            Dim i As UInteger
            For i = 0 To lpr.numSupportedCountries - 1
                lpr.set_countryWeight(i, 0)
            Next

            lpr.set_countryWeight(countryId, 1.0F)

            lpr.realizeCountryWeights()

            'Set the product key (if supplied).

            If args.Count = 4 Then
                lpr.set_productKey(args(3))
            End If

            'Create an instance of 'IProcessor'.

            Dim proc As SimpleLPR3.IProcessor
            proc = lpr.createProcessor()
            proc.plateRegionDetectionEnabled = True
            proc.cropToPlateRegionEnabled = True

            'Process the source file.

            Console.WriteLine("Processing...")
            Dim cds As System.Collections.Generic.List(Of SimpleLPR3.Candidate)
            cds = proc.analyze(args(2))

            If cds.Count = 0 Then

                Console.WriteLine("No license plate found")

            Else

                Console.WriteLine("{0} license plate candidates found:", cds.Count)

                ' Iterate over all candidates.

                For Each cd As SimpleLPR3.Candidate In cds

                    Console.WriteLine("***********")
                    Console.WriteLine("Light background: {0}, left: {1}, top: {2}, width: {3}, height: {4}", cd.brightBackground, cd.bbox.Left, cd.bbox.Top, cd.bbox.Width, cd.bbox.Height)
                    Console.WriteLine("Plate confidence: {0}. Plate vertices: {1}", cd.plateDetectionConfidence, String.Join(", ", cd.plateRegionVertices))
                    Console.WriteLine("Matches:")

                    ' Iterate over all country matches.

                    For Each match As SimpleLPR3.CountryMatch In cd.matches

                        Console.WriteLine("-----------")
                        Console.WriteLine("Text: {0}, country: {1}, ISO: {2}, confidence: {3}", match.text, match.country, match.countryISO, match.confidence)
                        Console.WriteLine("Elements:")

                        For Each e As SimpleLPR3.Element In match.elements
                            Console.WriteLine("Glyph: {0}, confidence: {1}, left: {2}, top: {3}, width: {4}, height: {5}",
                                          e.glyph, e.confidence, e.bbox.Left, e.bbox.Top, e.bbox.Width, e.bbox.Height)
                        Next
                    Next
                Next
            End If

        Else

            ' Wrong number of arguments, display usage.

            Console.WriteLine()
            Console.WriteLine("Usage:  {0} <country id> <source file> [product key]", IO.Path.GetFileName(Process.GetCurrentProcess.MainModule.FileName))
            Console.WriteLine("    Parameters:")
            Console.WriteLine("     country id: Country identifier. The allowed values are")

            Dim ui As UInteger
            For ui = 0 To lpr.numSupportedCountries - 1
                Console.WriteLine("         {0}   : {1}", ui, lpr.get_countryCode(ui))
            Next ui

            Console.WriteLine("     source file:  image file to be processed")
            Console.WriteLine("     product key:  product key file. this value is optional, if not provided SimpleLPR will run in evaluation mode")

        End If
    End Sub

End Module