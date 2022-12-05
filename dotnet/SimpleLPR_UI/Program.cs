﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SimpleLPR_UI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SimpleLPR_UI());
        }
    }
}
