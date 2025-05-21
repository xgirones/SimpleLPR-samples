using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SimpleLPR3;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using Avalonia.Platform;

namespace VideoANPR.Services
{
    /// <summary>
    /// Represents the metadata for a tracked license plate that will be saved to file.
    /// </summary>
    public class TrackedPlateMetadata
    {
        /// <summary>
        /// The recognized license plate text.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The country code where this plate format is used.
        /// </summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>
        /// The ISO country code.
        /// </summary>
        public string CountryISO { get; set; } = string.Empty;

        /// <summary>
        /// The confidence score of the recognition (0.0 to 1.0).
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Timestamp of the first detection of this plate.
        /// </summary>
        public TimeSpan FirstDetectionTime { get; set; }

        /// <summary>
        /// Timestamp of the most recent detection of this plate.
        /// </summary>
        public TimeSpan NewestDetectionTime { get; set; }

        /// <summary>
        /// Timestamp of the representative frame used for this track.
        /// </summary>
        public TimeSpan RepresentativeTime { get; set; }

        /// <summary>
        /// Frame ID of the first detection.
        /// </summary>
        public uint FirstDetectionFrameId { get; set; }

        /// <summary>
        /// Frame ID of the most recent detection.
        /// </summary>
        public uint NewestDetectionFrameId { get; set; }

        /// <summary>
        /// Frame ID of the representative frame.
        /// </summary>
        public uint RepresentativeFrameId { get; set; }

        /// <summary>
        /// Bounding box of the license plate in the representative frame.
        /// </summary>
        public BoundingBox PlateRegion { get; set; } = new BoundingBox();

        /// <summary>
        /// Whether the plate has dark text on light background.
        /// </summary>
        public bool BrightBackground { get; set; }

        /// <summary>
        /// Confidence that this candidate corresponds to a real license plate.
        /// </summary>
        public float PlateDetectionConfidence { get; set; }

        /// <summary>
        /// Information about individual characters in the license plate.
        /// </summary>
        public List<CharacterInfo> Characters { get; set; } = new List<CharacterInfo>();

        /// <summary>
        /// All possible country matches for this plate.
        /// </summary>
        public List<CountryMatchInfo> AllMatches { get; set; } = new List<CountryMatchInfo>();

        /// <summary>
        /// Path to the saved thumbnail image file.
        /// </summary>
        public string ThumbnailPath { get; set; } = string.Empty;

        /// <summary>
        /// Original video file path (if available).
        /// </summary>
        public string VideoPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a bounding box with coordinates.
    /// </summary>
    public class BoundingBox
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Represents information about a single character in the license plate.
    /// </summary>
    public class CharacterInfo
    {
        /// <summary>
        /// The recognized character.
        /// </summary>
        public char Character { get; set; }

        /// <summary>
        /// Confidence score for this character recognition.
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Bounding box of this character in the image.
        /// </summary>
        public BoundingBox BoundingBox { get; set; } = new BoundingBox();
    }

    /// <summary>
    /// Represents a country match for the license plate.
    /// </summary>
    public class CountryMatchInfo
    {
        /// <summary>
        /// The license plate text for this country interpretation.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The country name.
        /// </summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>
        /// The ISO country code.
        /// </summary>
        public string CountryISO { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this country match.
        /// </summary>
        public float Confidence { get; set; }
    }

    /// <summary>
    /// Service responsible for logging tracked license plates to disk.
    /// </summary>
    public class PlateLoggingService
    {
        /// <summary>
        /// Gets or sets the output directory where plate data will be saved.
        /// If null or empty, no logging will occur.
        /// </summary>
        public string? OutputDirectory { get; set; }

        /// <summary>
        /// Gets or sets whether logging is enabled.
        /// </summary>
        public bool LoggingEnabled { get; set; } = true;

        /// <summary>
        /// JSON serializer options for consistent formatting.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,  // Pretty-print the JSON
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,  // Use camelCase for property names
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Logs a tracked license plate candidate to disk.
        /// </summary>
        /// <param name="track">The tracked plate candidate to log.</param>
        /// <param name="videoPath">The path to the source video file (optional).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task LogTrackedPlateAsync(ITrackedPlateCandidate track, string? videoPath = null)
        {
            // Skip logging if not enabled or no output directory specified
            if (!LoggingEnabled || string.IsNullOrWhiteSpace(OutputDirectory))
                return;

            try
            {
                // Generate base filename using timestamp and plate text
                string baseFileName = GenerateBaseFileName(track);

                // Save thumbnail image
                string thumbnailPath = await SaveThumbnailAsync(track, baseFileName);

                // Save metadata
                await SaveMetadataAsync(track, baseFileName, thumbnailPath, videoPath);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - logging shouldn't crash the application
                System.Diagnostics.Debug.WriteLine($"Error logging tracked plate: {ex.Message}");
                // You might want to use a proper logging framework here
            }
        }

        /// <summary>
        /// Generates a base filename for the tracked plate files.
        /// </summary>
        /// <param name="track">The tracked plate candidate.</param>
        /// <returns>A filename-safe base name for the files.</returns>
        private static string GenerateBaseFileName(ITrackedPlateCandidate track)
        {
            // Get the representative timestamp
            var timestamp = TimeSpan.FromSeconds(track.firstDetectionTimestamp);

            // Get the plate text (use first match if available)
            string plateText = "UNKNOWN";
            if (track.representativeCandidate.matches.Count > 0)
            {
                plateText = track.representativeCandidate.matches[0].text;
            }

            // Clean the plate text to make it filename-safe
            string safeText = SanitizeForFileName(plateText);

            // Create filename with timestamp and plate text
            string baseFileName = $"{track.firstDetectionFrameId:D6}_{timestamp.ToString(@"hh\hmm\mss\s")}_{safeText}";

            return baseFileName;
        }

        /// <summary>
        /// Sanitizes a string to make it safe for use in filenames.
        /// </summary>
        /// <param name="input">The input string to sanitize.</param>
        /// <returns>A filename-safe version of the input string.</returns>
        private static string SanitizeForFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "EMPTY";

            // Remove or replace invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            string result = input;

            foreach (char c in invalidChars)
            {
                result = result.Replace(c, '_');
            }

            // Replace spaces with underscores and limit length
            result = result.Replace(' ', '_').Trim();

            // Limit length to reasonable maximum
            if (result.Length > 50)
            {
                result = result.Substring(0, 50);
            }

            return string.IsNullOrWhiteSpace(result) ? "SANITIZED" : result;
        }

        /// <summary>
        /// Saves the thumbnail image of the tracked plate.
        /// </summary>
        /// <param name="track">The tracked plate candidate.</param>
        /// <param name="baseFileName">The base filename to use.</param>
        /// <returns>The path to the saved thumbnail file.</returns>
        private async Task<string> SaveThumbnailAsync(ITrackedPlateCandidate track, string baseFileName)
        {
            string thumbnailPath = Path.Combine(OutputDirectory!, $"{baseFileName}.jpg");

            // Get the thumbnail from the track
            var thumbnail = track.representativeThumbnail;
            if (thumbnail != null)
            {
                // Use SimpleLPR's built-in method to save as JPEG
                // Using quality 95 for high-quality output (range: 0-100, or -1 for default)
                await Task.Run(() => thumbnail.saveAsJPEG(thumbnailPath, 95));
            }
            else
            {
                // If no thumbnail is available, create a placeholder file
                string placeholderPath = thumbnailPath.Replace(".jpg", "_no_thumbnail.txt");
                await File.WriteAllTextAsync(placeholderPath,
                    "No thumbnail data available for this track.");
                thumbnailPath = placeholderPath;
            }

            return thumbnailPath;
        }

        /// <summary>
        /// Saves the metadata of the tracked plate as a JSON file.
        /// </summary>
        /// <param name="track">The tracked plate candidate.</param>
        /// <param name="baseFileName">The base filename to use.</param>
        /// <param name="thumbnailPath">The path to the saved thumbnail.</param>
        /// <param name="videoPath">The path to the source video file.</param>
        private async Task SaveMetadataAsync(ITrackedPlateCandidate track, string baseFileName, string thumbnailPath, string? videoPath)
        {
            string metadataPath = Path.Combine(OutputDirectory!, $"{baseFileName}.json");

            // Create metadata object from the tracked plate
            var metadata = CreateMetadataFromTrack(track, thumbnailPath, videoPath);

            // Serialize to JSON and save
            string jsonContent = JsonSerializer.Serialize(metadata, JsonOptions);
            await File.WriteAllTextAsync(metadataPath, jsonContent);
        }

        /// <summary>
        /// Creates a metadata object from a tracked plate candidate.
        /// </summary>
        /// <param name="track">The tracked plate candidate.</param>
        /// <param name="thumbnailPath">The path to the saved thumbnail.</param>
        /// <param name="videoPath">The path to the source video file.</param>
        /// <returns>A metadata object with all relevant information.</returns>
        private static TrackedPlateMetadata CreateMetadataFromTrack(ITrackedPlateCandidate track, string thumbnailPath, string? videoPath)
        {
            var candidate = track.representativeCandidate;

            var metadata = new TrackedPlateMetadata
            {
                // Basic information
                FirstDetectionTime = TimeSpan.FromSeconds(track.firstDetectionTimestamp),
                NewestDetectionTime = TimeSpan.FromSeconds(track.newestDetectionTimestamp),
                RepresentativeTime = TimeSpan.FromSeconds(track.representativeTimestamp),
                FirstDetectionFrameId = track.firstDetectionFrameId,
                NewestDetectionFrameId = track.newestDetectionFrameId,
                RepresentativeFrameId = track.representativeFrameId,

                // Plate information
                BrightBackground = candidate.brightBackground,
                PlateDetectionConfidence = candidate.plateDetectionConfidence,

                // Plate region
                PlateRegion = new BoundingBox
                {
                    X = candidate.bbox.X,
                    Y = candidate.bbox.Y,
                    Width = candidate.bbox.Width,
                    Height = candidate.bbox.Height
                },

                // File paths
                ThumbnailPath = Path.GetFileName(thumbnailPath),  // Store relative path
                VideoPath = videoPath ?? string.Empty
            };

            // Process all country matches
            foreach (var match in candidate.matches)
            {
                var countryMatch = new CountryMatchInfo
                {
                    Text = match.text,
                    Country = match.country,
                    CountryISO = match.countryISO,
                    Confidence = match.confidence
                };

                metadata.AllMatches.Add(countryMatch);
            }

            // Use the first (best) match for primary information
            if (candidate.matches.Count > 0)
            {
                var bestMatch = candidate.matches[0];
                metadata.Text = bestMatch.text;
                metadata.Country = bestMatch.country;
                metadata.CountryISO = bestMatch.countryISO;
                metadata.Confidence = bestMatch.confidence;

                // Process individual characters
                foreach (var element in bestMatch.elements)
                {
                    var charInfo = new CharacterInfo
                    {
                        Character = element.glyph,
                        Confidence = element.confidence,
                        BoundingBox = new BoundingBox
                        {
                            X = element.bbox.X,
                            Y = element.bbox.Y,
                            Width = element.bbox.Width,
                            Height = element.bbox.Height
                        }
                    };

                    metadata.Characters.Add(charInfo);
                }
            }

            return metadata;
        }
    }
}