#include <windows.h>
#include <stdio.h>
#include <io.h>
#include <fcntl.h>
#include "AppWindow.h"
#include "WebViewHost.h"
#include "InteropHelper.h"
#include "ImmersiveEngine.h"

using namespace Immersive;

// Global Application State
void* g_engineHandle = nullptr;
WebViewHost g_webViewHost;
AppWindow g_appWindow;

// Bridge: C# -> C++ -> JS
void __stdcall OnImmersiveMessage(const char* json) {
    if (json) {
        printf("[DLL -> C++] State Push: %s\n", json);
        std::wstring jsonWide = InteropHelper::Utf8ToWide(json);
        if (!jsonWide.empty()) {
            std::wstring script = L"if(window.onStateChangedFromDll) { window.onStateChangedFromDll(" + jsonWide + L"); }";
            g_webViewHost.ExecuteScript(script);
        }
    }
}

int APIENTRY wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPWSTR lpCmdLine, _In_ int nCmdShow) {
    // 0. Enable DPI Awareness
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    // 0.1 Allocate Debug Console
    AllocConsole();
    FILE* fp;
    freopen_s(&fp, "CONOUT$", "w", stdout);
    freopen_s(&fp, "CONOUT$", "w", stderr);
    printf("--- Immersive Display Debug Console ---\n");

    // 1. Initialize COM
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    // 2. Initialize C# DLL Engine
    g_engineHandle = immersive_create();
    if (!g_engineHandle) {
        MessageBoxW(NULL, L"Failed to create C# Engine instance.", L"Fatal Error", MB_OK | MB_ICONERROR);
        return -1;
    }
    printf("[C++] C# Engine created.\n");

    // 3. Create Main Window with DPI Scaling
    UINT dpi = GetDpiForSystem();
    int width = MulDiv(450, dpi, 96);
    int height = MulDiv(850, dpi, 96);
    
    if (!g_appWindow.Create(hInstance, L"Immersive Display - C++ Native Host", width, height)) {
        return -1;
    }

    // 4. Setup WebView2
    g_webViewHost.SetMessageCallback([](const std::string& utf8Msg) {
        printf("[JS -> C++] Received: %s\n", utf8Msg.c_str());
        
        // JS -> C++ -> C#
        const char* response = immersive_handle_message(g_engineHandle, utf8Msg.c_str());
        if (response) {
            printf("[C# -> C++] Method Response: %s\n", response);
            // Push results back immediately to sync state if needed
            OnImmersiveMessage(response);
            immersive_free_string(response);
        }
    });

    g_appWindow.SetResizeCallback([]() {
        g_webViewHost.Resize(g_appWindow.GetHwnd());
    });

    g_appWindow.SetDestroyCallback([]() {
        if (g_engineHandle) {
            immersive_dispose(g_engineHandle);
            g_engineHandle = nullptr;
        }
    });

    // 5. Initialize WebView and DLL
    g_webViewHost.Initialize(g_appWindow.GetHwnd(), []() {
        printf("[C++] WebView2 Initialized.\n");
        
        // Bind C# events to our C++ handler
        immersive_initialize(g_engineHandle, OnImmersiveMessage);

        // Load the local UI
        std::wstring indexPath = InteropHelper::GetWebUiPath();
        printf("[C++] Navigating to: %S\n", indexPath.c_str());
        g_webViewHost.Navigate(InteropHelper::PathToUri(indexPath));
    });

    // 6. Enter Message Loop
    return g_appWindow.Run();
}
