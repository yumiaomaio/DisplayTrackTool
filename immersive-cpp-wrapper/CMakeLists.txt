cmake_minimum_required(VERSION 3.10)
project(host)
if(MSVC)
    add_compile_definitions(UNICODE _UNICODE)
endif()
set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(WV2_PKG_DIR "${CMAKE_CURRENT_SOURCE_DIR}/packages/microsoft.web.webview2/1.0.3967.48")
set(CS_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../ImmersiveDisplay/bin/$<CONFIG>/net10.0-windows")

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
target_include_directories(host PRIVATE "${WV2_PKG_DIR}/build/native/include")
target_compile_definitions(host PRIVATE UNICODE _UNICODE)
set_target_properties(host PROPERTIES
    CXX_STANDARD 23
    CXX_STANDARD_REQUIRED ON
)

target_link_libraries(host
    "${WV2_PKG_DIR}/build/native/x64/WebView2Loader.dll.lib"
    User32 Shell32 Ole32 Shlwapi
)

# Post-build: copy host.dll and WebView2Loader.dll to C# output directory
add_custom_command(TARGET host POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:host>" "${CS_OUTPUT_DIR}/"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${WV2_PKG_DIR}/build/native/x64/WebView2Loader.dll" "${CS_OUTPUT_DIR}/"
    COMMENT "Copying host.dll and WebView2Loader.dll to C# output..."
)
