namespace SimpleLPR_UI
{
    partial class SimpleLPR_UI
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SimpleLPR_UI));
            this.imgPlate = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.txtTgFile = new System.Windows.Forms.TextBox();
            this.dlgSaveTarget = new System.Windows.Forms.SaveFileDialog();
            this.butBrowseTg = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.txtInputFolder = new System.Windows.Forms.TextBox();
            this.butBrowseInput = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.cbCountry = new System.Windows.Forms.ComboBox();
            this.butStart = new System.Windows.Forms.Button();
            this.butStop = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.txtPlate = new System.Windows.Forms.TextBox();
            this.butKeep = new System.Windows.Forms.Button();
            this.butSkip = new System.Windows.Forms.Button();
            this.dlgBrowseInputFolder = new System.Windows.Forms.FolderBrowserDialog();
            this.lblPlate = new System.Windows.Forms.TextBox();
            this.butBack = new System.Windows.Forms.Button();
            this.ttToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.butBrowsePK = new System.Windows.Forms.Button();
            this.txtProductKey = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.dlgBrosePK = new System.Windows.Forms.OpenFileDialog();
            this.chbPaintBoxes = new System.Windows.Forms.CheckBox();
            this.udContrastSensitivity = new System.Windows.Forms.NumericUpDown();
            this.chbCropToPlateRegion = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.imgPlate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.udContrastSensitivity)).BeginInit();
            this.SuspendLayout();
            // 
            // imgPlate
            // 
            this.imgPlate.Location = new System.Drawing.Point(0, -1);
            this.imgPlate.Name = "imgPlate";
            this.imgPlate.Size = new System.Drawing.Size(640, 480);
            this.imgPlate.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.imgPlate.TabIndex = 0;
            this.imgPlate.TabStop = false;
            this.imgPlate.WaitOnLoad = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 535);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "Target File:";
            this.ttToolTip.SetToolTip(this.label1, "XML file where license plate candidates are to be written.");
            // 
            // txtTgFile
            // 
            this.txtTgFile.Location = new System.Drawing.Point(80, 531);
            this.txtTgFile.Name = "txtTgFile";
            this.txtTgFile.Size = new System.Drawing.Size(174, 20);
            this.txtTgFile.TabIndex = 2;
            this.ttToolTip.SetToolTip(this.txtTgFile, "XML file where license plate candidates are to be written.");
            this.txtTgFile.Validated += new System.EventHandler(this.txtTgFile_Validated);
            // 
            // butBrowseTg
            // 
            this.butBrowseTg.Location = new System.Drawing.Point(261, 530);
            this.butBrowseTg.Name = "butBrowseTg";
            this.butBrowseTg.Size = new System.Drawing.Size(30, 23);
            this.butBrowseTg.TabIndex = 3;
            this.butBrowseTg.Text = "...";
            this.ttToolTip.SetToolTip(this.butBrowseTg, "XML file where license plate candidates are to be written.");
            this.butBrowseTg.UseVisualStyleBackColor = true;
            this.butBrowseTg.Click += new System.EventHandler(this.butBrowseTg_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(306, 535);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Input Folder:";
            this.ttToolTip.SetToolTip(this.label2, "Folder containing valid image files to be processed.");
            // 
            // txtInputFolder
            // 
            this.txtInputFolder.Location = new System.Drawing.Point(381, 531);
            this.txtInputFolder.Name = "txtInputFolder";
            this.txtInputFolder.Size = new System.Drawing.Size(174, 20);
            this.txtInputFolder.TabIndex = 5;
            this.ttToolTip.SetToolTip(this.txtInputFolder, "Folder containing valid image files to be processed.");
            this.txtInputFolder.Validated += new System.EventHandler(this.txtInputFolder_Validated);
            // 
            // butBrowseInput
            // 
            this.butBrowseInput.Location = new System.Drawing.Point(561, 530);
            this.butBrowseInput.Name = "butBrowseInput";
            this.butBrowseInput.Size = new System.Drawing.Size(30, 23);
            this.butBrowseInput.TabIndex = 6;
            this.butBrowseInput.Text = "...";
            this.ttToolTip.SetToolTip(this.butBrowseInput, "Folder containing valid image files to be processed.");
            this.butBrowseInput.UseVisualStyleBackColor = true;
            this.butBrowseInput.Click += new System.EventHandler(this.butBrowseInput_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 564);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(46, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Country:";
            this.ttToolTip.SetToolTip(this.label3, "Target country. Only license plates complying with the selected country syntax wi" +
        "ll be shown. ");
            // 
            // cbCountry
            // 
            this.cbCountry.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbCountry.FormattingEnabled = true;
            this.cbCountry.Items.AddRange(new object[] {
            "Germany",
            "Spain",
            "UK_GreatBritain"});
            this.cbCountry.Location = new System.Drawing.Point(80, 560);
            this.cbCountry.Name = "cbCountry";
            this.cbCountry.Size = new System.Drawing.Size(174, 21);
            this.cbCountry.TabIndex = 8;
            this.ttToolTip.SetToolTip(this.cbCountry, "Target country. Only license plates complying with the selected country syntax wi" +
        "ll be shown. ");
            // 
            // butStart
            // 
            this.butStart.Enabled = false;
            this.butStart.Location = new System.Drawing.Point(435, 630);
            this.butStart.Name = "butStart";
            this.butStart.Size = new System.Drawing.Size(75, 23);
            this.butStart.TabIndex = 9;
            this.butStart.Text = "Start";
            this.ttToolTip.SetToolTip(this.butStart, "Start processing license plates in the input folder.");
            this.butStart.UseVisualStyleBackColor = true;
            this.butStart.Click += new System.EventHandler(this.butStart_Click);
            // 
            // butStop
            // 
            this.butStop.Enabled = false;
            this.butStop.Location = new System.Drawing.Point(516, 630);
            this.butStop.Name = "butStop";
            this.butStop.Size = new System.Drawing.Size(75, 23);
            this.butStop.TabIndex = 10;
            this.butStop.Text = "Stop";
            this.ttToolTip.SetToolTip(this.butStop, "Stop processing and write results to the target file.");
            this.butStop.UseVisualStyleBackColor = true;
            this.butStop.Click += new System.EventHandler(this.butStop_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(306, 599);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(74, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Plate Number:";
            // 
            // txtPlate
            // 
            this.txtPlate.Enabled = false;
            this.txtPlate.Location = new System.Drawing.Point(381, 595);
            this.txtPlate.Name = "txtPlate";
            this.txtPlate.Size = new System.Drawing.Size(172, 20);
            this.txtPlate.TabIndex = 12;
            this.ttToolTip.SetToolTip(this.txtPlate, "The license plate text that will be written to the target file. Modify it in case" +
        " the license plate detection went wrong.");
            // 
            // butKeep
            // 
            this.butKeep.Enabled = false;
            this.butKeep.Location = new System.Drawing.Point(95, 632);
            this.butKeep.Name = "butKeep";
            this.butKeep.Size = new System.Drawing.Size(75, 23);
            this.butKeep.TabIndex = 13;
            this.butKeep.Text = "&Keep";
            this.ttToolTip.SetToolTip(this.butKeep, "Keep the value in the \'Plate Number\' text box and move to the next license plate " +
        "in the input folder.");
            this.butKeep.UseVisualStyleBackColor = true;
            this.butKeep.Click += new System.EventHandler(this.butKeep_Click);
            // 
            // butSkip
            // 
            this.butSkip.Enabled = false;
            this.butSkip.Location = new System.Drawing.Point(177, 632);
            this.butSkip.Name = "butSkip";
            this.butSkip.Size = new System.Drawing.Size(75, 23);
            this.butSkip.TabIndex = 14;
            this.butSkip.Text = "&Skip";
            this.ttToolTip.SetToolTip(this.butSkip, "Skip the current license plate and move to the next license plate in the input fo" +
        "lder.");
            this.butSkip.UseVisualStyleBackColor = true;
            this.butSkip.Click += new System.EventHandler(this.butSkip_Click);
            // 
            // lblPlate
            // 
            this.lblPlate.Enabled = false;
            this.lblPlate.Font = new System.Drawing.Font("Arial", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblPlate.Location = new System.Drawing.Point(161, 479);
            this.lblPlate.Name = "lblPlate";
            this.lblPlate.Size = new System.Drawing.Size(317, 44);
            this.lblPlate.TabIndex = 15;
            this.lblPlate.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.ttToolTip.SetToolTip(this.lblPlate, "Text of the license plate candidate with the highest confidence factor.");
            // 
            // butBack
            // 
            this.butBack.Enabled = false;
            this.butBack.Location = new System.Drawing.Point(258, 632);
            this.butBack.Name = "butBack";
            this.butBack.Size = new System.Drawing.Size(75, 23);
            this.butBack.TabIndex = 16;
            this.butBack.Text = "&Back";
            this.ttToolTip.SetToolTip(this.butBack, "Move back to the previous license plate.");
            this.butBack.UseVisualStyleBackColor = true;
            this.butBack.Click += new System.EventHandler(this.butBack_Click);
            // 
            // butBrowsePK
            // 
            this.butBrowsePK.Location = new System.Drawing.Point(561, 560);
            this.butBrowsePK.Name = "butBrowsePK";
            this.butBrowsePK.Size = new System.Drawing.Size(30, 23);
            this.butBrowsePK.TabIndex = 19;
            this.butBrowsePK.Text = "...";
            this.ttToolTip.SetToolTip(this.butBrowsePK, "Product key file. Leave it blank to use SimpleLPR in evaluation mode.");
            this.butBrowsePK.UseVisualStyleBackColor = true;
            this.butBrowsePK.Click += new System.EventHandler(this.butBrowsePK_Click);
            // 
            // txtProductKey
            // 
            this.txtProductKey.Location = new System.Drawing.Point(381, 561);
            this.txtProductKey.Name = "txtProductKey";
            this.txtProductKey.Size = new System.Drawing.Size(174, 20);
            this.txtProductKey.TabIndex = 18;
            this.ttToolTip.SetToolTip(this.txtProductKey, "Product key file. Leave it blank to use SimpleLPR in evaluation mode.");
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(306, 565);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(68, 13);
            this.label5.TabIndex = 17;
            this.label5.Text = "Product Key:";
            this.ttToolTip.SetToolTip(this.label5, "Product key file. Leave it blank to use SimpleLPR in evaluation mode.");
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 599);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(99, 13);
            this.label6.TabIndex = 22;
            this.label6.Text = "Contrast Sensitivity:";
            this.ttToolTip.SetToolTip(this.label6, "Folder containing valid image files to be processed.");
            // 
            // chbPaintBoxes
            // 
            this.chbPaintBoxes.AutoSize = true;
            this.chbPaintBoxes.Checked = true;
            this.chbPaintBoxes.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chbPaintBoxes.Enabled = false;
            this.chbPaintBoxes.Location = new System.Drawing.Point(505, 485);
            this.chbPaintBoxes.Name = "chbPaintBoxes";
            this.chbPaintBoxes.Size = new System.Drawing.Size(129, 17);
            this.chbPaintBoxes.TabIndex = 20;
            this.chbPaintBoxes.Text = "Draw bounding boxes";
            this.chbPaintBoxes.UseVisualStyleBackColor = true;
            this.chbPaintBoxes.CheckedChanged += new System.EventHandler(this.chbPaintBoxes_CheckedChanged);
            // 
            // udContrastSensitivity
            // 
            this.udContrastSensitivity.DecimalPlaces = 3;
            this.udContrastSensitivity.Enabled = false;
            this.udContrastSensitivity.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this.udContrastSensitivity.Location = new System.Drawing.Point(122, 595);
            this.udContrastSensitivity.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.udContrastSensitivity.Name = "udContrastSensitivity";
            this.udContrastSensitivity.Size = new System.Drawing.Size(132, 20);
            this.udContrastSensitivity.TabIndex = 21;
            this.udContrastSensitivity.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.udContrastSensitivity.ValueChanged += new System.EventHandler(this.udContrastSensitivity_ValueChanged);
            // 
            // chbCropToPlateRegion
            // 
            this.chbCropToPlateRegion.AutoSize = true;
            this.chbCropToPlateRegion.Enabled = false;
            this.chbCropToPlateRegion.Location = new System.Drawing.Point(505, 503);
            this.chbCropToPlateRegion.Name = "chbCropToPlateRegion";
            this.chbCropToPlateRegion.Size = new System.Drawing.Size(118, 17);
            this.chbCropToPlateRegion.TabIndex = 23;
            this.chbCropToPlateRegion.Text = "Crop to plate region";
            this.chbCropToPlateRegion.UseVisualStyleBackColor = true;
            this.chbCropToPlateRegion.CheckedChanged += new System.EventHandler(this.chbCropToPlateRegion_CheckedChanged);
            // 
            // SimpleLPR_UI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 661);
            this.Controls.Add(this.chbCropToPlateRegion);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.udContrastSensitivity);
            this.Controls.Add(this.chbPaintBoxes);
            this.Controls.Add(this.butBrowsePK);
            this.Controls.Add(this.txtProductKey);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.butBack);
            this.Controls.Add(this.lblPlate);
            this.Controls.Add(this.butSkip);
            this.Controls.Add(this.butKeep);
            this.Controls.Add(this.txtPlate);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.butStop);
            this.Controls.Add(this.butStart);
            this.Controls.Add(this.cbCountry);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.butBrowseInput);
            this.Controls.Add(this.txtInputFolder);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.butBrowseTg);
            this.Controls.Add(this.txtTgFile);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.imgPlate);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "SimpleLPR_UI";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "SimpleLPR UI";
            ((System.ComponentModel.ISupportInitialize)(this.imgPlate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.udContrastSensitivity)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox imgPlate;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtTgFile;
        private System.Windows.Forms.SaveFileDialog dlgSaveTarget;
        private System.Windows.Forms.Button butBrowseTg;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtInputFolder;
        private System.Windows.Forms.Button butBrowseInput;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cbCountry;
        private System.Windows.Forms.Button butStart;
        private System.Windows.Forms.Button butStop;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtPlate;
        private System.Windows.Forms.Button butKeep;
        private System.Windows.Forms.Button butSkip;
        private System.Windows.Forms.FolderBrowserDialog dlgBrowseInputFolder;
        private System.Windows.Forms.TextBox lblPlate;
        private System.Windows.Forms.Button butBack;
        private System.Windows.Forms.ToolTip ttToolTip;
        private System.Windows.Forms.Button butBrowsePK;
        private System.Windows.Forms.TextBox txtProductKey;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.OpenFileDialog dlgBrosePK;
        private System.Windows.Forms.CheckBox chbPaintBoxes;
        private System.Windows.Forms.NumericUpDown udContrastSensitivity;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckBox chbCropToPlateRegion;
    }
}

