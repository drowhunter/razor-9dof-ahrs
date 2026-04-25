/******************************************************************************************
 * C# Wrapper for the Razor AHRS native library.
 *
 * Communicates with the Sparkfun 9DOF Razor IMU (SEN-10125 / SEN-10736) and
 * 9DOF Sensor Stick (SEN-10183 / SEN-10321 / SEN-10724) via a serial port.
 *
 * Requires the native shared library built from CSharp/Native/CMakeLists.txt:
 *   Linux : libRazorAHRSNative.so
 *   macOS : libRazorAHRSNative.dylib
 *
 * The library is only supported on Linux and macOS (the underlying C++ code
 * uses POSIX serial-port APIs).
 *
 * Released under GNU GPL (General Public License) v3.0
 * Copyright (C) 2013 Peter Bartz [http://ptrbrtz.net]
 * C# wrapper: https://github.com/drowhunter/razor-9dof-ahrs
 ******************************************************************************************/

using System;
using System.Runtime.InteropServices;

namespace RazorAHRS
{
    /// <summary>
    /// Data output mode for the Razor AHRS sensor.
    /// </summary>
    public enum RazorMode
    {
        /// <summary>
        /// Yaw, pitch, and roll angles expressed in degrees.
        /// The data callback receives 3 floats: [yaw, pitch, roll].
        /// </summary>
        YawPitchRoll = 0,

        /// <summary>
        /// Raw (uncalibrated) sensor readings from accelerometer, magnetometer,
        /// and gyroscope.  The data callback receives 9 floats:
        /// [acc_x, acc_y, acc_z, mag_x, mag_y, mag_z, gyr_x, gyr_y, gyr_z].
        /// </summary>
        AccMagGyrRaw = 1,

        /// <summary>
        /// Calibrated sensor readings from accelerometer, magnetometer, and
        /// gyroscope.  The data callback receives 9 floats with the same layout
        /// as <see cref="AccMagGyrRaw"/>.
        /// </summary>
        AccMagGyrCalibrated = 2
    }

    /// <summary>
    /// C# wrapper around the native Razor AHRS C++ library.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create an instance to connect to a Razor AHRS tracker over a serial port.
    /// Serial I/O runs in a background thread managed by the native library;
    /// the <see cref="DataReceived"/> and <see cref="ErrorOccurred"/> events are
    /// therefore raised on that background thread – marshal to the UI thread if
    /// needed.
    /// </para>
    /// <para>
    /// Always dispose the instance when done to stop the background thread and
    /// close the serial port.
    /// </para>
    /// </remarks>
    public sealed class RazorAHRS : IDisposable
    {
        // Name of the native shared library (without platform prefix/suffix).
        // The .NET runtime resolves "RazorAHRSNative" to:
        //   Linux : libRazorAHRSNative.so
        //   macOS : libRazorAHRSNative.dylib
        private const string NativeLibName = "RazorAHRSNative";

        // ------------------------------------------------------------------ //
        //  P/Invoke declarations
        // ------------------------------------------------------------------ //

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeDataCallback(IntPtr data, int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeErrorCallback(
            [MarshalAs(UnmanagedType.LPStr)] string message);

        [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl,
                   EntryPoint = "razor_create")]
        private static extern IntPtr RazorCreate(
            [MarshalAs(UnmanagedType.LPStr)] string port,
            int mode,
            int connectTimeoutMs,
            int baudRate,
            NativeDataCallback  dataCallback,
            NativeErrorCallback errorCallback);

        [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl,
                   EntryPoint = "razor_destroy")]
        private static extern void RazorDestroy(IntPtr handle);

        [DllImport(NativeLibName, CallingConvention = CallingConvention.Cdecl,
                   EntryPoint = "razor_get_last_error")]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private static extern string RazorGetLastError();

        // ------------------------------------------------------------------ //
        //  Instance state
        // ------------------------------------------------------------------ //

        // Stored as fields to prevent the GC from collecting them while the
        // native library holds unmanaged function pointers to them.
        private readonly NativeDataCallback  _nativeDataCallback;
        private readonly NativeErrorCallback _nativeErrorCallback;

        private IntPtr _handle;
        // volatile ensures the background-thread callbacks see the most recent
        // value written by Dispose() on the calling thread.
        private volatile bool _disposed;

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Raised when a complete data frame is received from the tracker.
        /// The event argument is an array of floats whose length and meaning
        /// depend on the <see cref="RazorMode"/> specified at construction:
        /// <list type="bullet">
        ///   <item><see cref="RazorMode.YawPitchRoll"/> – 3 floats: yaw, pitch, roll (degrees)</item>
        ///   <item><see cref="RazorMode.AccMagGyrRaw"/> or <see cref="RazorMode.AccMagGyrCalibrated"/>
        ///         – 9 floats: acc x/y/z, mag x/y/z, gyr x/y/z</item>
        /// </list>
        /// <para><b>Note:</b> raised on the native background thread.</para>
        /// </summary>
        public event EventHandler<float[]>? DataReceived;

        /// <summary>
        /// Raised when an error occurs in the native background thread.
        /// <para><b>Note:</b> raised on the native background thread.</para>
        /// </summary>
        public event EventHandler<string>? ErrorOccurred;

        /// <summary>
        /// Creates a Razor AHRS tracker and starts the background I/O thread.
        /// </summary>
        /// <param name="port">
        /// Serial port path, e.g. <c>"/dev/ttyUSB0"</c> on Linux or
        /// <c>"/dev/tty.usbserial-XXXXXX"</c> on macOS.
        /// </param>
        /// <param name="mode">Data output mode.</param>
        /// <param name="connectTimeoutMs">
        /// Timeout in milliseconds for the initial connection handshake.
        /// Defaults to 5000 ms.
        /// </param>
        /// <param name="baudRate">
        /// Serial-port baud rate.  Supported values: 9600, 19200, 38400, 57600,
        /// 115200.  Defaults to 57600 (the Razor firmware default).
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="port"/> is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the native library cannot open the port or the tracker
        /// does not respond within the timeout.
        /// </exception>
        public RazorAHRS(string port,
                         RazorMode mode,
                         int connectTimeoutMs = 5000,
                         int baudRate = 57600)
        {
            if (string.IsNullOrEmpty(port))
                throw new ArgumentException("Port cannot be null or empty.", nameof(port));

            // Keep the delegate objects alive for the lifetime of this instance
            // so the GC does not collect them while native code holds pointers.
            _nativeDataCallback  = OnNativeData;
            _nativeErrorCallback = OnNativeError;

            _handle = RazorCreate(port, (int)mode, connectTimeoutMs, baudRate,
                                  _nativeDataCallback, _nativeErrorCallback);

            if (_handle == IntPtr.Zero)
            {
                string nativeError = RazorGetLastError() ?? "Unknown error";
                throw new InvalidOperationException(
                    $"Failed to connect to Razor AHRS on '{port}': {nativeError}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Native callbacks (called from the background thread)
        // ------------------------------------------------------------------ //

        private void OnNativeData(IntPtr dataPtr, int count)
        {
            if (_disposed) return;

            float[] data = new float[count];
            Marshal.Copy(dataPtr, data, 0, count);
            DataReceived?.Invoke(this, data);
        }

        private void OnNativeError(string message)
        {
            if (_disposed) return;
            ErrorOccurred?.Invoke(this, message);
        }

        // ------------------------------------------------------------------ //
        //  IDisposable
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Stops the background I/O thread and closes the serial port.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_handle != IntPtr.Zero)
            {
                RazorDestroy(_handle);
                _handle = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>Finalizer – ensures native resources are released.</summary>
        ~RazorAHRS()
        {
            if (_handle != IntPtr.Zero)
            {
                RazorDestroy(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
