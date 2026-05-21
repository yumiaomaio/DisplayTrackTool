cmake_minimum_required(VERSION 3.10)
project(immersive-cpp)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# --- Configuration Paths ---
set(FRONTEND_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../vite-project")
set(CS_PROJ_PATH "${CMAKE_CURRENT_SOURCE_DIR}/../ImmersiveDisplay/ImmersiveDisplay.csproj")
set(CS_PUBLISH_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../ImmersiveDisplay/bin/Release/net10.0-windows/win-x64/publish")
set(CS_NATIVE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../ImmersiveDisplay/bin/Release/net10.0-windows/win-x64/native")
set(WV2_PKG_DIR "${CMAKE_CURRENT_SOURCE_DIR}/packages/microsoft.web.webview2/1.0.3967.48")

# --- 1. Automatic Frontend (NPM) Build Step ---
# This target runs 'npm run build' to generate the WebUI assets.
find_program(NPM_EXECUTABLE npm REQUIRED)
add_custom_target(rebuild_frontend
    COMMAND ${NPM_EXECUTABLE} run build
    WORKING_DIRECTORY "${FRONTEND_DIR}"
    COMMENT "Compiling Frontend (Vue/Vite)..."
)

# --- 2. Automatic C# AOT Build Step ---
add_custom_target(rebuild_csharp
    COMMAND dotnet publish "${CS_PROJ_PATH}" -c Release -r win-x64 --self-contained
    WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/.."
    COMMENT "Compiling C# Native AOT DLL..."
)

# --- 3. Include directories ---
include_directories(include)
include_directories("${WV2_PKG_DIR}/build/native/include")

# --- 4. Source Files ---
set(SOURCES 
    src/main.cpp 
    src/AppWindow.cpp 
    src/WebViewHost.cpp 
    src/InteropHelper.cpp
    app.manifest
)

# --- 5. Add executable ---
add_executable(immersive-cpp WIN32 ${SOURCES})

# Ensure both C# and Frontend are built before C++
add_dependencies(immersive-cpp rebuild_csharp rebuild_frontend)

# --- 6. Link libraries ---
target_link_libraries(immersive-cpp 
    "${CS_NATIVE_DIR}/ImmersiveDisplay.lib"
    "${WV2_PKG_DIR}/build/native/x64/WebView2Loader.dll.lib"
    User32
    Shell32
    Ole32
    Shlwapi
)

# --- 7. Post-build: Copy DLLs and assets ---
add_custom_command(TARGET immersive-cpp POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${WV2_PKG_DIR}/build/native/x64/WebView2Loader.dll" $<TARGET_FILE_DIR:immersive-cpp>
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "${CS_PUBLISH_DIR}/ImmersiveDisplay.dll" $<TARGET_FILE_DIR:immersive-cpp>
    # Note: Copy WebUI from the frontend's dist folder instead of the .NET project
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${FRONTEND_DIR}/dist" $<TARGET_FILE_DIR:immersive-cpp>/WebUI
    COMMENT "Syncing DLLs and WebUI assets to output directory..."
)
