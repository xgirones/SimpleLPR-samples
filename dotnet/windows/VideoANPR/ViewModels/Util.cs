/* 
VideoANPR - Automatic Number Plate Recognition for Video Streams

VideoANPR is a sample C# application that showcases the capabilities of the SimpleLPR ANPR library for processing video streams.
It demonstrates how to leverage computer vision techniques to detect and extract license plate information in real-time.

Author: Xavier Gironés (xavier.girones@warelogic.com)

Features:
- ANPR Processing: VideoANPR utilizes the SimpleLPR ANPR library to perform automatic number plate recognition on video streams.
- Video Capture: The application uses SimpleLPR's native video capture capabilities.
- Multi-platform User Interface: VideoANPR utilizes WPF and ReactiveUI to provide a Windows user interface.

Requirements:
- .NET Core SDK 6.0 or higher
- SimpleLPR ANPR library
- WPF and ReactiveUI

Contributions and feedback are welcome! If you encounter any issues, have suggestions for improvements, or want to add new features,
please submit a pull request or open an issue on the GitHub repository.

Disclaimer: VideoANPR is intended for educational and research purposes only.
*/

using System;
using System.Drawing;
using SimpleLPR3;

namespace VideoANPR.ViewModels
{
    public static class Util
    {
        /// <summary>
        /// Retrieves the vertices of the license plate region based on the given candidate.
        /// </summary>
        /// <param name="lp">The candidate representing a license plate.</param>
        /// <returns>An array of points representing the vertices of the license plate region.</returns>
        public static System.Drawing.Point[] GetPlateRegionVertices(Candidate lp)
        {
            System.Drawing.Point[] plateRegionVertices;

            // There are two possible cases to obtain the license plate region:
            if (lp.plateDetectionConfidence > 0 &&
                lp.plateRegionVertices.Length == 4 &&
                lp.plateRegionVertices[0].X >= 0 && lp.plateRegionVertices[0].Y >= 0 &&
                lp.plateRegionVertices[1].X >= 0 && lp.plateRegionVertices[1].Y >= 0 &&
                lp.plateRegionVertices[2].X >= 0 && lp.plateRegionVertices[2].Y >= 0 &&
                lp.plateRegionVertices[3].X >= 0 && lp.plateRegionVertices[3].Y >= 0)
            {
                // 1. The engine considers that the candidate corresponds to a license plate and it has been able to regress the plate boundary.
                plateRegionVertices = lp.plateRegionVertices;
            }
            else
            {
                // 2. Only text has been found.
                // In this case we employ the bounding box of the text as the plate region.

                // We add a safety margin calculated as the 12.5% of the largest dimension.
                int nMaxDim = Math.Max(lp.bbox.Width, lp.bbox.Width);
                int nExtraSize = nMaxDim / 8;

                Rectangle bb = lp.bbox;
                bb.Inflate(nExtraSize, nExtraSize);

                // Compute the coordinates of the rectangle.
                plateRegionVertices = new System.Drawing.Point[]
                {
                    new System.Drawing.Point(bb.Left, bb.Top),
                    new System.Drawing.Point(bb.Right, bb.Top),
                    new System.Drawing.Point(bb.Right, bb.Bottom),
                    new System.Drawing.Point(bb.Left, bb.Bottom)
                };
            }

            return plateRegionVertices;
        }
    }
}