/******************************************************************************************
 * Compatibility header for <tr1/functional> on modern C++ compilers.
 * Included before all sources via the CMake -include flag so that the original
 * RazorAHRS.h (which uses std::tr1::function) compiles without modification on
 * systems where <tr1/functional> has been removed (GCC 14+, modern libc++).
 ******************************************************************************************/

#pragma once

#ifdef __has_include
#  if !__has_include(<tr1/functional>)
#    include <functional>
     namespace std {
       namespace tr1 {
         using std::function;
         using std::bind;
         using std::ref;
         using std::cref;
         namespace placeholders = std::placeholders;
       }
     }
#  endif
#endif
