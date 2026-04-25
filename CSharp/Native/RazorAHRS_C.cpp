/******************************************************************************************
 * C API wrapper implementation for the Razor AHRS C++ library.
 *
 * Released under GNU GPL (General Public License) v3.0
 ******************************************************************************************/

#include "RazorAHRS_C.h"
#include "RazorAHRS.h"

#include <string>
#include <pthread.h>
#include <termios.h>  // speed_t, B57600, etc.

/* --------------------------------------------------------------------------
 * Internal state
 * -------------------------------------------------------------------------- */

/** Holds the C++ object together with the caller-supplied C callbacks. */
struct RazorWrapper
{
  RazorAHRS*        razor;
  RazorDataCallback data_cb;
  RazorErrorCallback error_cb;
  int               data_count;  // 3 for YPR, 9 for raw/calibrated
};

/* Last error message (set by razor_create on failure). */
static std::string      s_last_error;
static pthread_mutex_t  s_error_mutex = PTHREAD_MUTEX_INITIALIZER;

static void set_last_error(const std::string& msg)
{
  pthread_mutex_lock(&s_error_mutex);
  s_last_error = msg;
  pthread_mutex_unlock(&s_error_mutex);
}

/** Map a plain integer baud rate to the corresponding POSIX speed_t constant. */
static speed_t int_to_speed(int baud_rate)
{
  switch (baud_rate)
  {
    case 9600:   return B9600;
    case 19200:  return B19200;
    case 38400:  return B38400;
    case 115200: return B115200;
    case 57600:
    default:     return B57600;
  }
}

/* --------------------------------------------------------------------------
 * Public C API
 * -------------------------------------------------------------------------- */

extern "C"
{

RazorHandle razor_create(const char*        port,
                         int                mode,
                         int                connect_timeout_ms,
                         int                baud_rate,
                         RazorDataCallback  data_cb,
                         RazorErrorCallback error_cb)
{
  if (!port || port[0] == '\0')
  {
    set_last_error("No port specified.");
    return nullptr;
  }

  RazorAHRS::Mode razor_mode;
  int data_count;

  switch (mode)
  {
    case RAZOR_MODE_YAW_PITCH_ROLL:
      razor_mode = RazorAHRS::YAW_PITCH_ROLL;
      data_count = 3;
      break;
    case RAZOR_MODE_ACC_MAG_GYR_RAW:
      razor_mode = RazorAHRS::ACC_MAG_GYR_RAW;
      data_count = 9;
      break;
    case RAZOR_MODE_ACC_MAG_GYR_CALIBRATED:
      razor_mode = RazorAHRS::ACC_MAG_GYR_CALIBRATED;
      data_count = 9;
      break;
    default:
      set_last_error("Unknown mode value.");
      return nullptr;
  }

  RazorWrapper* wrapper = new RazorWrapper();
  wrapper->razor      = nullptr;
  wrapper->data_cb    = data_cb;
  wrapper->error_cb   = error_cb;
  wrapper->data_count = data_count;

  try
  {
    /* Lambda captures: wrapper pointer is stable for the lifetime of the
     * RazorAHRS object, so it is safe to capture by pointer here.        */
    auto on_data = [wrapper](const float* data)
    {
      if (wrapper->data_cb)
        wrapper->data_cb(data, wrapper->data_count);
    };

    auto on_error = [wrapper](const std::string& msg)
    {
      if (wrapper->error_cb)
        wrapper->error_cb(msg.c_str());
    };

    wrapper->razor = new RazorAHRS(
        std::string(port),
        on_data,
        on_error,
        razor_mode,
        connect_timeout_ms,
        int_to_speed(baud_rate));
  }
  catch (const std::runtime_error& e)
  {
    set_last_error(e.what());
    delete wrapper;
    return nullptr;
  }
  catch (...)
  {
    set_last_error("Unknown error while creating RazorAHRS object.");
    delete wrapper;
    return nullptr;
  }

  return static_cast<RazorHandle>(wrapper);
}

void razor_destroy(RazorHandle handle)
{
  if (!handle) return;

  RazorWrapper* wrapper = static_cast<RazorWrapper*>(handle);
  delete wrapper->razor;   // stops background thread
  wrapper->razor = nullptr;
  delete wrapper;
}

const char* razor_get_last_error(void)
{
  /* The string is owned by the library; the caller must not free it. */
  return s_last_error.c_str();
}

} /* extern "C" */
