#include <windows.h>
#include <print>
#include <format>
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

// Configuration: window size in device-independent pixels (DIPs)
static constexpr int kWindowWidthDips = 435;
static constexpr int kWindowHeightDips = 850;
static constexpr int kWindowDpiBase = 96;

// Helper to print truncated JSON for debugging (prevents Base64 flooding)
void SmartPrint(std::string_view prefix, const char* json) {
    if (!json) return;
    std::string_view s = json;
    
    if (s.length() > 256) {
        // If it looks like it contains a base64 image, truncate it heavily
        if (size_t base64Pos = s.find("data:image/"); base64Pos != std::string::npos) {
            std::println("{} {}... [Base64 Data Truncated] }}", prefix, s.substr(0, base64Pos + 20));
            return;
        }
        // General long string truncation
        std::println("{} {}... (truncated, total length: {})", prefix, s.substr(0, 256), s.length());
    }
    else {
        std::println("{} {}", prefix, json);
    }
}

// Bridge: C# -> C++ -> JS
void __stdcall OnImmersiveMessage(const char* json) {
    if (json) {
        SmartPrint("[DLL -> C++] State Push:", json);
        std::wstring jsonWide = InteropHelper::Utf8ToWide(json);
        if (!jsonWide.empty()) {
            std::wstring script = std::format(L"if(window.onStateChangedFromDll) {{ window.onStateChangedFromDll({}); }}", jsonWide);
            g_webViewHost.ExecuteScript(script);
        }
    }
}

int APIENTRY wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPWSTR lpCmdLine, _In_ int nCmdShow) {
    // 0. Enable DPI Awareness
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    // 0.1 Attach to parent console (cmd/PowerShell) if available
    bool hasConsole = AttachConsole(ATTACH_PARENT_PROCESS);
    FILE* fp{};
    if (hasConsole) {
        freopen_s(&fp, "CONOUT$", "w", stdout);
        freopen_s(&fp, "CONOUT$", "w", stderr);
        std::println("--- Immersive Display Debug Console (C++23) ---");
    } else {
        // Redirect to nul to keep std::println safe when launched from Explorer
        freopen_s(&fp, "nul", "w", stdout);
        freopen_s(&fp, "nul", "w", stderr);
    }
    (void)fp;

    // 1. Initialize COM
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    // 2. Initialize C# DLL Engine
    g_engineHandle = immersive_create();
    if (!g_engineHandle) {
        MessageBoxW(NULL, L"Failed to create C# Engine instance.", L"Fatal Error", MB_OK | MB_ICONERROR);
        return -1;
    }
    std::println("[C++] C# Engine created.");

    // 3. Create Main Window with DPI Scaling
    UINT dpi = GetDpiForSystem();
    int width = MulDiv(kWindowWidthDips, dpi, kWindowDpiBase);
    int height = MulDiv(kWindowHeightDips, dpi, kWindowDpiBase);
    
    if (!g_appWindow.Create(hInstance, L"Immersive Display", width, height)) {
        return -1;
    }

    // 4. Setup WebView2
    g_webViewHost.SetMessageCallback([](const std::string& utf8Msg) {
        SmartPrint("[JS -> C++] Received:", utf8Msg.c_str());
        
        // JS -> C++ -> C#
        const char* response = immersive_handle_message(g_engineHandle, utf8Msg.c_str());
        if (response) {
            SmartPrint("[C# -> C++] Method Response:", response);
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
        std::println("[C++] WebView2 Initialized.");
        
        // Bind C# events to our C++ handler
        immersive_initialize(g_engineHandle, OnImmersiveMessage);

        // Load the local UI
        std::wstring indexPath = InteropHelper::GetWebUiPath();
        std::println("[C++] Navigating to: {}", InteropHelper::WideToUtf8(indexPath.c_str()));
        g_webViewHost.Navigate(InteropHelper::PathToUri(indexPath));
    });

    // 6. Enter Message Loop
    int result = g_appWindow.Run();

    CoUninitialize();
    if (hasConsole) FreeConsole();
    return result;
}
