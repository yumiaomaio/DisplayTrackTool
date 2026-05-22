cmake_minimum_required(VERSION 3.14)
project(host)
if(MSVC)
    add_compile_definitions(UNICODE _UNICODE)
endif()
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ============================================================
# 自动下载 WebView2 SDK (NuGet 包)
# ============================================================
include(FetchContent)
set(WEBVIEW2_VERSION "1.0.3967.48")
FetchContent_Declare(
    webview2
    URL "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/${WEBVIEW2_VERSION}"
)
FetchContent_MakeAvailable(webview2)
message(STATUS "WebView2 SDK: ${webview2_SOURCE_DIR}")

# ============================================================
# host.dll — WebView2 host library consumed by the C# EXE
# ============================================================
set(HOST_SOURCES
    src/host.cpp
    src/AppWindow.cpp
    src/WebViewHost.cpp
    src/InteropHelper.cpp
)

add_library(host SHARED ${HOST_SOURCES})

target_include_directories(host PRIVATE include)
target_include_directories(host PRIVATE "${webview2_SOURCE_DIR}/build/native/include")
target_compile_definitions(host PRIVATE UNICODE _UNICODE)
set_target_properties(host PROPERTIES
    CXX_STANDARD 23
    CXX_STANDARD_REQUIRED ON
)

target_link_libraries(host
    "${webview2_SOURCE_DIR}/build/native/x64/WebView2Loader.dll.lib"
    User32 Shell32 Ole32 Shlwapi
)
