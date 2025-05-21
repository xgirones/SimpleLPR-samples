/* 
VideoANPR - Automatic Number Plate Recognition for Video Streams

VideoANPR is a sample C# application that showcases the capabilities of the SimpleLPR ANPR library for processing video streams.
It demonstrates how to leverage computer vision techniques to detect and extract license plate information in real-time.

Author: Xavier Gironés (xavier.girones@warelogic.com)

Features:
- ANPR Processing: VideoANPR utilizes the SimpleLPR ANPR library to perform automatic number plate recognition on video streams.
- Video Capture: The application uses SimpleLPR's native video capture capabilities.
- Multi-platform User Interface: VideoANPR utilizes Avalonia and ReactiveUI to provide a cross-platform user interface,
  enabling the application to run on both Windows and Linux systems seamlessly.

Requirements:
- .NET Core SDK 6.0 or higher
- SimpleLPR ANPR library
- Avalonia and ReactiveUI

Contributions and feedback are welcome! If you encounter any issues, have suggestions for improvements, or want to add new features,
please submit a pull request or open an issue on the GitHub repository.

Disclaimer: VideoANPR is intended for educational and research purposes only.
*/

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System;
using System.Xml.Linq;
using VideoANPR.ViewModels;

namespace VideoANPR
{
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            Control? control = null;

            if (data != null)
            {
                var name = data.GetType().FullName!.Replace("ViewModel", "View");
                var type = Type.GetType(name);

                control = (type != null) ?
                        (Control)Activator.CreateInstance(type)! :
                        new TextBlock { Text = "Not Found: " + name };
            }
            else
            {
                control = new TextBlock { Text = "Not Found: (null)" };
            }

            return control;
        }                     

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}