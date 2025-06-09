# SimpleLPR Code Samples

A collection of sample applications demonstrating [SimpleLPR](https://www.warelogic.com), a handy license plate recognition (LPR/ANPR) library for C++, .NET, and Python.

![Screen capture of the VideoANPR demo application](https://www.warelogic.com/images/screenshot_videoanpr_1.jpg)

## What is SimpleLPR?

SimpleLPR is a professional-grade library that automatically detects and reads vehicle license plates in images and video streams. With over 10 years of development and hundreds of production deployments, it provides:

- 🎯 **85-95% accuracy** under typical conditions
- 🌍 **Support for ~90 countries** with region-specific templates  
- 🚀 **High-performance** multi-threaded processing
- 📹 **Real-time video** analysis with temporal tracking
- 🔧 **Simple API** that just works

## What's in This Repository?

This repository contains ready-to-run examples showcasing SimpleLPR's capabilities:

### 📁 Sample Applications

#### Comprehensive Examples
The repository covers all major SimpleLPR features through practical examples:
- Single image analysis
- Batch processing of multiple images
- Video file processing
- RTSP stream analysis
- Multi-threaded processing with pools
- Plate tracking demonstrations

#### Application Types

**Console Applications**
- Basic image processing examples
- Command-line tools for batch operations
- Video processing with plate tracking across frames

**GUI Applications**
- **WPF Application** (Windows only) - Full-featured desktop application with real-time video display
- **Avalonia Application** (Cross-platform) - Modern UI application that runs on Windows and Linux

#### Language Support
All core functionality is available in multiple languages:
- **C# (.NET)** - Complete set of examples from basic to advanced
- **Visual Basic (.NET)** - Basic SimpleLPR functionality in VB.NET syntax
- **Python** - Comprehensive samples with Pythonic API

Each language provides equivalent functionality, so you can choose based on your preferences and existing codebase.

### 🎯 Perfect for Learning

Each example includes detailed comments explaining:
- How to initialize the SimpleLPR engine
- Country-specific configuration
- Image and video processing workflows
- Result interpretation
- Best practices for production use

## Quick Start

### Prerequisites

- **SimpleLPR SDK** - [60-day evaluation available](https://www.warelogic.com)
- **Operating System** - Windows 10/11 or Ubuntu 20.04+ (x64)
- **Development Tools**:
  - .NET: Visual Studio 2019+ or .NET SDK
  - Python: Python 3.8, 3.9, 3.10, 3.11, or 3.12

### 🔵 Running .NET Samples

#### Using Visual Studio
1. Open any `.csproj` (C#) or `.vbproj` (VB.NET) file in Visual Studio 2019 or later
2. Build and run (F5)

#### Using Command Line
```bash
# Navigate to any sample directory containing a .csproj or .vbproj file
cd [sample_directory]
dotnet build
dotnet run [arguments]
```

Most console samples expect arguments in this format:
```bash
dotnet run <image_or_video_path> <country_id> [product_key]
```

Example:
```bash
dotnet run parking_lot.jpg 82 license.xml  # 82 = Spain
```

The GUI applications can be run without arguments and provide interactive interfaces.

### 🐍 Running Python Samples

1. Install SimpleLPR from PyPI:
```bash
pip install SimpleLPR
```

2. Navigate to the Python samples directory and explore the available examples:
```bash
cd [python_samples_directory]
python [sample_name].py
```

Most Python samples include built-in help and usage instructions. Run them without arguments to see available options.

### 🏷️ Country Codes

SimpleLPR supports license plates from approximately 90 countries. Here are some commonly used ones:

| ID | Country | ID | Country | ID | Country | ID | Country |
|----|---------|----|---------|----|---------|----|---------| 
| 5  | Austria | 16 | Canada  | 32 | France  | 45 | Italy   |
| 8  | Belgium | 19 | Colombia| 34 | Germany | 63 | Netherlands |
| 12 | Brazil  | 28 | Ecuador | 41 | India   | 71 | Poland  |
| 14 | Cambodia| 30 | Estonia | 43 | Ireland | 82 | Spain   |
| 15 | Cameroon| 31 | Finland | 44 | Israel  | 90 | UK-GreatBritain |

**Special Templates:**
- IDs 97-102: Numerical recognition (3-8 digits)
- ID 46: Italy-Moped, ID 47: Italy-Vintage
- ID 50: Kemler Code (hazardous materials)

Run any sample without arguments to see the complete list of all supported countries.

## Key Features Demonstrated

### 🖼️ Image Processing
- Load images from files or memory
- Detect multiple plates in a single image
- Get plate text, confidence scores, and locations
- Extract individual character positions

### 📹 Video Processing
- Process video files (MP4, AVI, etc.)
- Connect to RTSP/HTTP streams
- Real-time analysis with minimal latency
- Automatic frame buffering and synchronization

### 🔄 Plate Tracking
- Track plates across multiple frames
- Reduce false positives through temporal correlation
- Handle temporary occlusions
- Generate consolidated results per vehicle

### ⚡ Performance Optimization
- Multi-threaded processor pools
- Concurrent stream processing
- GPU acceleration (CUDA version)
- Configurable frame size limits

## Example Output

```
SimpleLPR Version: 3.6.0.0
Processing: highway_video.mp4
Country: Spain

[NEW PLATE] Frame 42 @ 1.4s: 1234ABC (Spain) - Confidence: 0.987
[NEW PLATE] Frame 89 @ 3.0s: 5678XYZ (Spain) - Confidence: 0.923
[TRACK CLOSED] 1234ABC - Duration: 4.2s (126 frames)

Total plates detected: 47
Processing time: 150.3 seconds
Average speed: 20.2 fps
```

## Documentation

- 📖 **[User Guide](https://www.warelogic.com/doc/SimpleLPR3.pdf)** - Complete documentation
- 🔧 **[.NET API Reference](https://www.warelogic.com/doc/SimpleLPR.chm)** - Detailed class reference
- 🐍 **[Python API Reference](https://www.warelogic.com/doc/simplelpr_python_api_reference.htm)** - Python-specific documentation
- 🚀 **[Python Quick Start](https://www.warelogic.com/doc/simplelpr_python_quickstart_guide.htm)** - Get started quickly

## Need the SDK?

These samples require the SimpleLPR SDK. You can:

1. **[Download the evaluation](https://www.warelogic.com)** - 60-day trial with full features
2. **[Contact sales](mailto:support@warelogic.com)** - For licensing options

The SDK includes:
- Native libraries for C++, .NET, and Python
- Complete documentation
- Additional sample code
- Technical support

## License

The code samples in this repository are provided under the MIT License. SimpleLPR itself is commercial software - see [licensing options](https://www.warelogic.com) for details.

## Support

- 📧 **Email**: support@warelogic.com
- 🌐 **Website**: https://www.warelogic.com
- 📝 **Include**: Version info, error messages, and sample images when reporting issues

---

*SimpleLPR - Professional license plate recognition since 2009*
