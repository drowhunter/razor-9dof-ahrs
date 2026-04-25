cmake_minimum_required(VERSION 3.10)
project(RazorAHRSNative VERSION 1.4.2 LANGUAGES CXX)

# Require at least C++11 for lambda captures used in the C wrapper.
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Path to the original C++ sources.
set(CPP_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../C++")

add_library(RazorAHRSNative SHARED
    RazorAHRS_C.cpp
    "${CPP_DIR}/RazorAHRS.cpp"
)

target_include_directories(RazorAHRSNative PRIVATE
    "${CPP_DIR}"
    "${CMAKE_CURRENT_SOURCE_DIR}"
)

# The original code requires _REENTRANT for POSIX threads.
target_compile_definitions(RazorAHRSNative PRIVATE _REENTRANT)

# Inject compat.h before every source file so that the original RazorAHRS.h
# (which uses <tr1/functional>) still compiles on modern compilers where that
# header has been removed.
target_compile_options(RazorAHRSNative PRIVATE
    -include "${CMAKE_CURRENT_SOURCE_DIR}/compat.h"
)

# Link against pthreads.
find_package(Threads REQUIRED)
target_link_libraries(RazorAHRSNative PRIVATE Threads::Threads)

# Produce versioned shared library.
set_target_properties(RazorAHRSNative PROPERTIES
    VERSION   ${PROJECT_VERSION}
    SOVERSION 1
)

# Install target (optional).
include(GNUInstallDirs)
install(TARGETS RazorAHRSNative
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
)
install(FILES RazorAHRS_C.h
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)
