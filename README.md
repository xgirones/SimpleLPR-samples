This repository contains code examples for **SimpleLPR**, which is a library for vehicle license plate recognition. It has a very simple programming interface that allows applications to supply a path to an image or a buffer in memory and returns the detected license plate text and its location in the input image. It can be used from C++, .NET-enabled programming languages, or Python. 

Typical detection rates range between 85% and 95%, provided the license plates are in good condition, free of obstructions, and the text height is at least 20 pixels.

![Screen capture of the SimpleLPR_UI demo application](https://www.warelogic.com/images/screenshot6.jpg)

## Instructions

### .NET

The .NET samples can be built using Visual Studio 2019 or later. Alternatively, they can be built from the command prompt running

    dotnet build

 on the folders where the '.csproj' and '.vbproj' files are located.
At this time, the supported platforms are *windows-x86*, *windows-x64*, and *linux-x64*.

### Python

To run the python sample, first install the SimpleLPR package using 

    pip install SimpleLPR

Python wheels are provided for the *3.8* and *3.9* interpreters, and the *windows-x64* and *linux-x64* platforms.

### C/C++

 For C/C++ example code, plus the redistributable binaries, you can download [the full SDK](https://www.warelogic.com).
 
---

For further information, you can download the [user's guide](https://www.warelogic.com/doc/SimpleLPR3.pdf) and the [class reference for .NET](https://www.warelogic.com/doc/SimpleLPR.chm).

You can submit your questions/issues/bug reports/feedback to support@warelogic.com