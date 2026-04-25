# Razor AHRS – C# Wrapper

C# bindings for the [Razor AHRS C++ library](../C++), exposing the same
functionality as the original C++ interface via a managed .NET 8 class library.

## Architecture

```
CSharp/
├── Native/          # C-compatible shared library wrapping the C++ class
│   ├── compat.h     # tr1/functional compatibility shim for modern compilers
│   ├── RazorAHRS_C.h
│   ├── RazorAHRS_C.cpp
│   └── CMakeLists.txt
├── RazorAHRS/       # .NET 8 class library (P/Invoke into the native library)
│   ├── RazorAHRS.cs
│   └── RazorAHRS.csproj
├── Example/         # Console application demonstrating usage
│   ├── Program.cs
│   └── Example.csproj
└── RazorAHRS.slnx   # .NET solution
```

The C# library talks to the tracker via a two-layer stack:

```
C# application
    ↓  events / P/Invoke
RazorAHRS.dll  (managed .NET library)
    ↓  P/Invoke
libRazorAHRSNative.so/.dylib  (native C shared library)
    ↓  C++ constructor / callbacks
RazorAHRS (original C++ class, unchanged)
    ↓  POSIX serial port + pthreads
Tracker hardware
```

## Platform support

Linux and macOS only (the underlying C++ code uses POSIX serial-port and
pthreads APIs).  Windows is not supported.

## Prerequisites

| Tool | Minimum version |
|------|----------------|
| CMake | 3.10 |
| g++ / clang++ | C++11 support |
| .NET SDK | 8.0 |

## Build

### 1 – Native shared library

```bash
mkdir -p CSharp/Native/build
cd CSharp/Native/build
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)
```

This produces `libRazorAHRSNative.so` (Linux) or `libRazorAHRSNative.dylib`
(macOS) in the build directory.

Copy the library to a location where the .NET runtime can find it (e.g. the
same directory as the compiled application, or a path listed in
`LD_LIBRARY_PATH` / `DYLD_LIBRARY_PATH`):

```bash
# Linux example
cp libRazorAHRSNative.so /usr/local/lib/
sudo ldconfig
```

### 2 – .NET library and example

```bash
cd CSharp
dotnet build RazorAHRS.slnx
```

## Usage

```csharp
using RazorAHRS;

// Connect to the tracker.
// The background I/O thread starts immediately.
using var razor = new RazorAHRS.RazorAHRS(
    port:              "/dev/ttyUSB0",
    mode:              RazorMode.YawPitchRoll,
    connectTimeoutMs:  5000,
    baudRate:          57600);

// DataReceived is raised on the native background thread.
razor.DataReceived += (_, data) =>
{
    // YawPitchRoll: data[0]=yaw, data[1]=pitch, data[2]=roll (degrees)
    Console.WriteLine($"Yaw={data[0]:F1}  Pitch={data[1]:F1}  Roll={data[2]:F1}");
};

razor.ErrorOccurred += (_, msg) =>
    Console.WriteLine($"Error: {msg}");

Console.ReadLine();   // keep running until RETURN
// razor.Dispose() is called automatically by the using statement
```

For `AccMagGyrRaw` or `AccMagGyrCalibrated` modes the `data` array contains
9 floats: `[acc_x, acc_y, acc_z, mag_x, mag_y, mag_z, gyr_x, gyr_y, gyr_z]`.

> **Thread safety** – `DataReceived` and `ErrorOccurred` are raised from the
> native background thread.  If you need to update a UI element, marshal back
> to the UI thread (e.g. `Dispatcher.InvokeAsync` on WPF/WinUI,
> `Control.BeginInvoke` on WinForms, `SynchronizationContext.Post` in general).

## Running the example

1. Edit `Example/Program.cs` and set `serialPort` to the correct port for your
   system.
2. Build (step 2 above).
3. Run:

```bash
cd CSharp
dotnet run --project Example/Example.csproj
```
