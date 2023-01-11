using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Xml;
using SimpleLPR3;

namespace SimpleLPR_UI
{
    public partial class SimpleLPR_UI : Form
    {
        struct LPEntry
        {
            public string fileName;
            public string plate;
        }

        ISimpleLPR _lpr;
        IProcessor _proc;
        string _curFile;
        Bitmap _curBitmap;
        List<Candidate> _curCands;

        List<string> files;
        int enumF;

        List<LPEntry> lps;

        public SimpleLPR_UI()
        {
            InitializeComponent();

            cbCountry.SelectedIndex = 0;

            try
            {
                EngineSetupParms setupP;
                setupP.cudaDeviceId = -1; // Use CPU.
                setupP.enableImageProcessingWithGPU = false;
                setupP.enableClassificationWithGPU = false;
                setupP.maxConcurrentImageProcessingOps = 0;  // Use the default value.

                _lpr = SimpleLPR.Setup(setupP);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to initialize the SimpleLPR library", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                throw;
            }

            SimpleLPR3.VersionNumber ver = _lpr.versionNumber;
            this.Text = string.Format("SimpleLPR UI --- Version {0}.{1}.{2}.{3}", ver.A, ver.B, ver.C, ver.D);

            files = new List<string>();
            lps = new List<LPEntry>();

            cbCountry.Items.Clear();
            cbCountry.Sorted = true;
            for (uint i = 0; i < _lpr.numSupportedCountries; ++i)
            {
                string sCountry = _lpr.get_countryCode(i);
                cbCountry.Items.Add(sCountry);
            }
            cbCountry.SelectedIndex = 0;
        }

        private void drawImage()
        {
            if (chbPaintBoxes.Checked && _curCands != null && _curCands.Count > 0 )
            {           
                if (_curBitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Indexed)
                {
                    Bitmap bmp = new Bitmap(_curBitmap);

                    using (Graphics gfx = Graphics.FromImage(bmp))
                    {
                        gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bicubic;
                        gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                        Pen skyBluePen = new Pen(Brushes.DeepSkyBlue, 1.0f);
                        Pen springGreenPen = new Pen(Brushes.SpringGreen, 2.0f);
                        StringFormat sf = new StringFormat();
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        sf.FormatFlags = StringFormatFlags.NoClip;

                        foreach (Candidate cd in _curCands)
                        {
                            if (cd.plateDetectionConfidence > 0)
                            {
                                gfx.DrawPolygon(springGreenPen, cd.plateRegionVertices);
                            }

                            if (cd.matches.Count > 0)
                            {
                                CountryMatch cm = cd.matches[0];

                                foreach (Element e in cm.elements)
                                {
                                    gfx.DrawRectangle(skyBluePen, e.bbox);

                                    Color c1 = Color.FromKnownColor(KnownColor.Crimson);
                                    Color c2 = Color.FromKnownColor(KnownColor.Blue);

                                    double fLambda = e.confidence; // Math.Log((double)e.confidence + 1.0) / Math.Log(2.0);
                                    double fLambda_1 = 1.0 - fLambda;

                                    double fR = (double)c1.R * fLambda + (double)c2.R * fLambda_1;
                                    double fG = (double)c1.G * fLambda + (double)c2.G * fLambda_1;
                                    double fB = (double)c1.B * fLambda + (double)c2.B * fLambda_1;

                                    Color c3 = Color.FromArgb((int)fR, (int)fG, (int)fB);
                                    using (Brush brush = new SolidBrush(c3))
                                    {
                                        using (Font fnt = new Font("Tahoma", (float)e.bbox.Height, GraphicsUnit.Pixel))
                                        {
                                            gfx.DrawString(e.glyph.ToString(),
                                                            fnt,
                                                            brush,
                                                            (float)e.bbox.Left + (float)e.bbox.Width / 2.0f,
                                                            (float)e.bbox.Bottom + (float)e.bbox.Height * 1.2f / 2.0f,
                                                            sf);
                                        }
                                    }
                                }
                            }
                        }

                        gfx.Flush();
                    }

                    imgPlate.Image = bmp;
                }
                else
                {
                    imgPlate.Image = _curBitmap;
                }
            }
            else
            {
                imgPlate.Image = _curBitmap;
            }
        }

        private List<Candidate> analyzeBitmap(Bitmap bm)
        {
            List<Candidate> cds = null;

            Rectangle r = new Rectangle(0, 0, bm.Width, bm.Height);
            BitmapData bmd = null;

            // Only PixelFormat.Format24bppRgb and PixelFormat.Format8bppIndexed are supported.

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
                        cds = _proc.analyze_C3(bmd.Scan0,
                                               (uint)bmd.Stride,
                                               (uint)bm.Width,
                                               (uint)bm.Height,
                                               0.114f, 0.587f, 0.299f); // GDI++ INTERNAL LAYOUT IS BGR!!!
                    }
                    break;

                case PixelFormat.Format24bppRgb:
                    {
                        // Convert to RGB to grayscale employing the standard NTSC CRT coefficients.
                        // In spite of the pixel format description, the internal layout in memory is (A)BGR.

                        bmd = bm.LockBits(r, ImageLockMode.ReadOnly, bm.PixelFormat);
                        cds = _proc.analyze_C3(bmd.Scan0,
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
                        cds = _proc.analyze(bmd.Scan0,
                                            (uint)bmd.Stride,
                                            (uint)bm.Width,
                                            (uint)bm.Height);
                    }
                    break;
                default:
                    throw new Exception(String.Format("Unsupported pixel format: {0}", bm.PixelFormat.ToString()));
            }

            return cds;
        }

        private void analyzeCurrentFile()
        {
            Cursor.Current = Cursors.WaitCursor;
            _curCands = null;

            try
            {
                using (FileStream fs = new FileStream(_curFile, FileMode.Open, FileAccess.Read))
                {
                    using (Image imTmp = Image.FromStream(fs, true, true))
                    {

                        _curBitmap = new Bitmap((Bitmap)imTmp);
                    }
                }

                _curCands = analyzeBitmap(_curBitmap);

                drawImage();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Analyze method failed", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }

            Cursor.Current = Cursors.Default;

            CountryMatch bestMatch;
            bestMatch.confidence = -1.0f;
            bestMatch.text = "";

            if (_curCands != null && _curCands.Count > 0)
            {
                for (int i = 0; i < _curCands.Count; ++i)
                {
                    if (_curCands[i].matches.Count > 1)
                    {
                        if (_curCands[i].matches[0].confidence > bestMatch.confidence)
                            bestMatch = _curCands[i].matches[0];
                    }
                }
            }

            if (bestMatch.confidence > 0)
            {
                lblPlate.Text = bestMatch.text;
                txtPlate.Text = bestMatch.text;
            }
            else
            {
                lblPlate.Text = "";
                txtPlate.Clear();
            }
        }

        private void nextFile()
        {
            if ( enumF < files.Count )
            {
                txtPlate.Enabled = true;
                butKeep.Enabled = true;
                butSkip.Enabled = true;
                chbPaintBoxes.Enabled = true;
                chbCropToPlateRegion.Enabled = true;

                _curFile = files[ enumF++ ];

                analyzeCurrentFile();
            }
            else
            {
                lblPlate.Text = "";
                txtPlate.Enabled = false;
                txtPlate.Clear();
                butKeep.Enabled = false;
                butSkip.Enabled = false;
                chbPaintBoxes.Enabled = false;
                chbCropToPlateRegion.Enabled = false;

                _curCands = null;
            }

            butBack.Enabled = ( enumF > 1 );
        }

        private void butBrowseTg_Click(object sender, EventArgs e)
        {
            dlgSaveTarget.ShowDialog( this );
            txtTgFile.Focus();
            txtTgFile.Text = dlgSaveTarget.FileName;
            txtInputFolder.Focus();
        }

        private void butBrowseInput_Click(object sender, EventArgs e)
        {
            dlgBrowseInputFolder.ShowDialog(this);
            txtInputFolder.Focus();
            txtInputFolder.Text = dlgBrowseInputFolder.SelectedPath;
            txtProductKey.Focus();
        }

        private void butBrowsePK_Click(object sender, EventArgs e)
        {
            dlgBrosePK.ShowDialog(this);
            txtProductKey.Text = dlgBrosePK.FileName;
        }

        private void butStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (_proc == null)
                {
                    if (txtProductKey.Text.Length > 0)
                        _lpr.set_productKey(txtProductKey.Text);
                    _proc = _lpr.createProcessor();
                    _proc.plateRegionDetectionEnabled = true;
                    _proc.cropToPlateRegionEnabled = chbCropToPlateRegion.Checked;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to create processor object", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }

            if (_proc != null)
            {
                lps.Clear();
                files.Clear();

                System.IO.DirectoryInfo dInfo = new System.IO.DirectoryInfo(txtInputFolder.Text);
                foreach (System.IO.FileInfo f in dInfo.GetFiles("*.*",System.IO.SearchOption.AllDirectories))
                {
                    if ( f.Extension.ToLower() == ".jpg" || f.Extension.ToLower() == ".tif" ||
                         f.Extension.ToLower() == ".png" || f.Extension.ToLower() == ".bmp")
                    {
                        files.Add(f.FullName);
                    }
                }

                if (files.Count > 0)
                {
                    for (uint i = 0; i < _lpr.numSupportedCountries; ++i)
                    {
                        string sCountry = _lpr.get_countryCode(i);
                        _lpr.set_countryWeight(sCountry, sCountry == cbCountry.Text ? 1.0f : 0.0f);
                    }

                    enumF = 0;

                    txtTgFile.Enabled = false;
                    txtInputFolder.Enabled = false;
                    txtProductKey.Enabled = false;
                    butBrowseInput.Enabled = false;
                    butBrowseTg.Enabled = false;
                    butBrowsePK.Enabled = false;
                    cbCountry.Enabled = false;
                    butStart.Enabled = false;

                    butStop.Enabled = true;

                    Cursor.Current = Cursors.WaitCursor;
                    _lpr.realizeCountryWeights();
                    Cursor.Current = Cursors.Default;

                    nextFile();

                    udContrastSensitivity.Enabled = true;
                    udContrastSensitivity.Value = (decimal)_proc.contrastSensitivityFactor;
                }
            }
        }

        private void butStop_Click(object sender, EventArgs e)
        {
            XmlDocument xml = new XmlDocument();
            XmlProcessingInstruction basePI = xml.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\"");
            xml.AppendChild(basePI);
            XmlElement baseElem = xml.CreateElement("Results");
            xml.AppendChild(baseElem);

            baseElem.SetAttribute("country", cbCountry.Text);
            foreach (LPEntry lpe in lps)
            {
                XmlElement elem = xml.CreateElement("pic");
                elem.SetAttribute("path", lpe.fileName);
                elem.SetAttribute("lp", lpe.plate);

                baseElem.AppendChild(elem);
            }


            xml.Save(txtTgFile.Text);

            txtPlate.Enabled = false;
            txtPlate.Clear();
            butKeep.Enabled = false;
            butSkip.Enabled = false;
            butBack.Enabled = false;
            butStop.Enabled = false;
            chbPaintBoxes.Enabled = false;
            chbCropToPlateRegion.Enabled = false;

            txtTgFile.Enabled = true;
            txtInputFolder.Enabled = true;
            txtProductKey.Enabled = true;
            butBrowseInput.Enabled = true;
            butBrowseTg.Enabled = true;
            butBrowsePK.Enabled = true;
            cbCountry.Enabled = true;
            butStart.Enabled = true;
            udContrastSensitivity.Enabled = false;
        }

        private void butKeep_Click(object sender, EventArgs e)
        {
            if (txtPlate.Text.Length > 0)
            {
                LPEntry lpe;
                lpe.fileName = files[ enumF - 1 ];
                lpe.plate = txtPlate.Text;

                lps.Add(lpe);
            }

            nextFile();
        }

        private void butSkip_Click(object sender, EventArgs e)
        {
            nextFile();
        }

        private void butBack_Click(object sender, EventArgs e)
        {
            if ( lps.Count > 0 && enumF > 1 )
            {
                if (lps[lps.Count - 1].fileName == files[enumF - 2])
                    lps.RemoveAt(lps.Count - 1);
            }

            enumF -= 2;
            nextFile();
        }

        private void checkCanEnableStart()
        {
            bool bCanEnable = false;

            if (txtTgFile.Text.Length > 0 && txtInputFolder.Text.Length > 0)
            {   
                if ( System.IO.Directory.Exists(txtInputFolder.Text) )
                {
                    bool bValidTg = System.IO.File.Exists(txtTgFile.Text);
                    if (!bValidTg)
                    {
                        try
                        {
                            System.IO.FileStream fs = System.IO.File.Create(txtTgFile.Text, 1024, System.IO.FileOptions.DeleteOnClose);
                            fs.Close();
                            bValidTg = true;
                        }
                        catch (System.Exception)
                        {
                        }
                    }

                    bCanEnable = bValidTg;
                }
            }

            butStart.Enabled = bCanEnable;
        }

        private void txtTgFile_Validated(object sender, EventArgs e)
        {
            checkCanEnableStart();   
        }

        private void txtInputFolder_Validated(object sender, EventArgs e)
        {
            checkCanEnableStart();   
        }

        private void chbPaintBoxes_CheckedChanged(object sender, EventArgs e)
        {
            drawImage();
        }

        private void udContrastSensitivity_ValueChanged(object sender, EventArgs e)
        {
            if (_proc != null)
            {
                _proc.contrastSensitivityFactor = (float)udContrastSensitivity.Value;
                analyzeCurrentFile();
            }
        }

        private void chbCropToPlateRegion_CheckedChanged(object sender, EventArgs e)
        {
            if (_proc != null)
            {
                _proc.cropToPlateRegionEnabled = chbCropToPlateRegion.Checked;
                analyzeCurrentFile();
            }
        }
    }
}
