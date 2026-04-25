/******************************************************************************************
 * C API wrapper for the Razor AHRS C++ library.
 *
 * Exposes a plain-C interface so that the native shared library can be consumed
 * from any language that supports the C calling convention – in particular from
 * C# via P/Invoke.
 *
 * Build the shared library with CMakeLists.txt in this directory.
 *
 * Released under GNU GPL (General Public License) v3.0
 ******************************************************************************************/

#ifndef RAZORAHRS_C_H
#define RAZORAHRS_C_H

#ifdef __cplusplus
extern "C" {
#endif

/** Opaque handle returned by razor_create(). */
typedef void* RazorHandle;

/**
 * Callback invoked (from a background thread) when a complete data frame
 * has been received from the tracker.
 *
 * @param data   Pointer to an array of floats.  Length depends on the mode:
 *               RAZOR_MODE_YAW_PITCH_ROLL        -> 3 floats  (yaw, pitch, roll)
 *               RAZOR_MODE_ACC_MAG_GYR_RAW       -> 9 floats  (acc x/y/z, mag x/y/z, gyr x/y/z)
 *               RAZOR_MODE_ACC_MAG_GYR_CALIBRATED -> 9 floats (same layout as raw)
 * @param count  Number of elements in @p data.
 */
typedef void (*RazorDataCallback)(const float* data, int count);

/**
 * Callback invoked (from a background thread) when an error occurs.
 *
 * @param message  Null-terminated UTF-8 error description.  The pointer is
 *                 valid only for the duration of the callback.
 */
typedef void (*RazorErrorCallback)(const char* message);

/** Data output modes – must match RazorAHRS::Mode values. */
#define RAZOR_MODE_YAW_PITCH_ROLL          0
#define RAZOR_MODE_ACC_MAG_GYR_RAW         1
#define RAZOR_MODE_ACC_MAG_GYR_CALIBRATED  2

/**
 * Create a Razor AHRS tracker object and start the background I/O thread.
 *
 * @param port              Serial port path (e.g. "/dev/ttyUSB0").
 * @param mode              One of the RAZOR_MODE_* constants.
 * @param connect_timeout_ms  Connection timeout in milliseconds.
 * @param baud_rate         Baud rate as a plain integer (e.g. 57600).
 *                          Supported values: 9600, 19200, 38400, 57600, 115200.
 *                          Any unrecognised value falls back to 57600.
 * @param data_cb           Called on each received data frame (may be NULL).
 * @param error_cb          Called on errors (may be NULL).
 * @return  Opaque handle on success, or NULL on failure.
 *          Call razor_get_last_error() immediately after a NULL return to
 *          retrieve the error message.
 */
RazorHandle razor_create(const char*       port,
                         int               mode,
                         int               connect_timeout_ms,
                         int               baud_rate,
                         RazorDataCallback data_cb,
                         RazorErrorCallback error_cb);

/**
 * Stop the background thread and release all resources associated with @p handle.
 * After this call @p handle must not be used again.
 */
void razor_destroy(RazorHandle handle);

/**
 * Return the error message from the most recent failed razor_create() call.
 * The returned pointer is valid until the next call to razor_create() or
 * razor_destroy(), and must not be freed by the caller.
 * Returns an empty string if no error has occurred.
 */
const char* razor_get_last_error(void);

#ifdef __cplusplus
}
#endif

#endif /* RAZORAHRS_C_H */
