/* 
VideoANPR - Automatic Number Plate Recognition for Video Streams

VideoANPR is a sample C# application that showcases the capabilities of the SimpleLPR ANPR library for processing video streams.
It demonstrates how to leverage computer vision techniques to detect and extract license plate information in real-time.

Author: Xavier Gironés (xavier.girones@warelogic.com)

Features:
- ANPR Processing: VideoANPR utilizes the SimpleLPR ANPR library to perform automatic number plate recognition on video streams.
- Video Capture: The application uses Emgu.CV as a third-party library for video capture, providing a simple and convenient way
  to process video frames. However, it can be easily replaced with any other compatible library if desired.
- Multi-platform User Interface: VideoANPR utilizes Avalonia and ReactiveUI to provide a cross-platform user interface,
  enabling the application to run on both Windows and Linux systems seamlessly.

Requirements:
- .NET Core SDK 6.0 or higher
- SimpleLPR ANPR library
- Emgu.CV (or alternative third-party library for video capture)
- Avalonia and ReactiveUI

Contributions and feedback are welcome! If you encounter any issues, have suggestions for improvements, or want to add new features,
please submit a pull request or open an issue on the GitHub repository.

Disclaimer: VideoANPR is intended for educational and research purposes only.
*/

using System;

namespace VideoANPR.Observables
{
    /*
    Summary:
    The `LockableDisposableWrapper<T>` class provides a wrapper around a disposable object of type `T`. It ensures that the underlying object is accessed
    and disposed in a thread-safe manner by providing a lock mechanism. This class follows the Dispose pattern to properly release resources when the wrapper
    is disposed.

    Remarks:
    - The class is generic, allowing it to wrap any type that implements `IDisposable`.
    - The wrapper can be locked using the `Lock` property to synchronize access to the underlying object.
    - The wrapped object can be accessed through the `Inner` property.
    - The `Dispose` method is implemented to release the wrapped object's resources, and it can be called explicitly or by using the `using` statement.

    Usage:
    - Create an instance of `LockableDisposableWrapper<T>` by providing the object to be wrapped as a parameter.
    - Access the wrapped object through the `Inner` property.
    - Lock the wrapper using the `Lock` property to ensure thread-safe access to the wrapped object.
    - Dispose of the wrapper to release the resources of the wrapped object.
    - Use the `LockableDisposableWrap<T>` extension method to conveniently create instances of `LockableDisposableWrapper<T>`.
    */

    public class LockableDisposableWrapper<T> : IDisposable where T : class, IDisposable
    { 
        private readonly object lock_ = new object();
        private T? inner_ = null;

        // To detect redundant Dispose calls
        private bool bDisposed_ = false;

        public LockableDisposableWrapper(T? o)
        {
            inner_ = o;
        }

        public object Lock { get { return lock_; } }
        public T? Inner { get { return inner_; } }
        public bool Disposed { get { return bDisposed_; } }

        // Implementation of the Dispose pattern:

        // Finalizer
        ~LockableDisposableWrapper() => Dispose(false);

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool bDisposing)
        {
            if (!bDisposed_)
            {
                if (bDisposing)
                {
                    lock (lock_)
                    {
                        if (inner_ != null)
                        {
                            inner_.Dispose();
                            inner_ = null;
                        }
                    }
                }

                bDisposed_ = true;
            }
        }
    }
    public static class LockableDisposableWrapper
    {
        // Extension method to create a LockableDisposableWrapper<T> from an object.
        public static LockableDisposableWrapper<T> LockableDisposableWrap<T>(this T? o) where T : class, IDisposable
        {
            return new LockableDisposableWrapper<T>(o);
        }
    }
}
