/******************************************************************************************
 * C# Example for the Razor AHRS wrapper.
 *
 * Demonstrates how to connect to a Razor AHRS tracker and receive yaw/pitch/roll
 * data using the RazorAHRS C# library.
 *
 * Released under GNU GPL (General Public License) v3.0
 ******************************************************************************************/

using System;
using RazorAHRS;

// ── Configure your serial port here ──────────────────────────────────────────
//const string serialPort = "/dev/tty.usbserial-A700eEhN"; // macOS example
const string serialPort = "/dev/ttyUSB0";                  // Linux example
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine();
Console.WriteLine("  Razor AHRS C# example");
Console.WriteLine("  Press RETURN to connect to tracker. Press RETURN again to quit.");
Console.ReadLine();

Console.WriteLine("  Connecting...");
Console.WriteLine();

RazorAHRS.RazorAHRS? razor = null;

try
{
    // Create RazorAHRS object.  Serial I/O will run in a native background thread
    // and report data/errors via the events below.
    //
    // To receive raw or calibrated sensor data instead of yaw/pitch/roll, pass
    //   RazorMode.AccMagGyrRaw  or  RazorMode.AccMagGyrCalibrated
    // as the second argument.
    razor = new RazorAHRS.RazorAHRS(serialPort, RazorMode.YawPitchRoll);

    // ── Data callback ──────────────────────────────────────────────────────
    // NOTE: called from the native background thread – do not update UI here
    // without marshalling to the UI thread.
    razor.DataReceived += (_, data) =>
    {
        // YawPitchRoll mode: data[0]=yaw, data[1]=pitch, data[2]=roll (degrees)
        Console.WriteLine(
            $"  Yaw = {data[0],6:F1}      Pitch = {data[1],6:F1}      Roll = {data[2],6:F1}");

        // AccMagGyrRaw / AccMagGyrCalibrated mode example (9 floats):
        // Console.WriteLine(
        //     $"  ACC = {data[0],6:F1}, {data[1],6:F1}, {data[2],6:F1}" +
        //     $"        MAG = {data[3],7:F1}, {data[4],7:F1}, {data[5],7:F1}" +
        //     $"        GYR = {data[6],7:F1}, {data[7],7:F1}, {data[8],7:F1}");
    };

    // ── Error callback ─────────────────────────────────────────────────────
    razor.ErrorOccurred += (_, msg) =>
    {
        Console.WriteLine($"  ERROR: {msg}");
    };
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Could not create tracker: {ex.Message}");
    Console.WriteLine($"  Did you set your serial port in Program.cs?");
    return;
}

Console.ReadLine();  // wait for RETURN key

razor?.Dispose();
