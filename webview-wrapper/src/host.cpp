#include <windows.h>
#include <print>
#include <format>
#include <io.h>
#include <fcntl.h>
#include <fstream>
#include <string>
#include <algorithm>
#include "AppWindow.h"
#include "WebViewHost.h"
#include "InteropHelper.h"

#define WM_EXECUTE_SCRIPT (WM_USER + 1)

using namespace Immersive;

// Check if profiles.json has "enableFileLogging": true
bool IsDebugModeEnabled() {
    std::wstring exePath = InteropHelper::GetWebUiPath();
    auto pos = exePath.find_last_of(L"\\/");
    if (pos == std::wstring::npos) return false;
    
    std::wstring configPath = exePath.substr(0, pos) + L"\\..\\profiles.json";
    
    std::ifstream file(configPath);
    if (!file.is_open()) return false;
    
    std::string content((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
    
    // Strip spaces, tabs, and newlines to eliminate formatting differences
    content.erase(std::remove_if(content.begin(), content.end(), [](unsigned char x) {
        return ::isspace(x);
    }), content.end());
    
    return content.find("\"enableFileLogging\":true") != std::string::npos;
}

// Helper to truncate massive base64 data in logs
std::string FilterBase64(std::string_view original) {
    std::string s(original);

    // 1. Truncate data:image/...base64,... patterns (State Push from C#)
    size_t startPos = 0;
    while ((startPos = s.find("data:image/", startPos)) != std::string::npos) {
        size_t commaPos = s.find("base64,", startPos);
        if (commaPos != std::string::npos) {
            size_t endQuote = s.find('\"', commaPos);
            if (endQuote != std::string::npos) {
                s.replace(commaPos + 7, endQuote - (commaPos + 7), "...<base64 omitted>...");
            }
        }
        startPos += 11;
    }

    // 2. Truncate raw base64 in "data":"..." fields (ImportDroppedIcon from JS)
    //    Matches: "data":"<raw base64>"
    startPos = 0;
    while ((startPos = s.find("\"data\":\"", startPos)) != std::string::npos) {
        size_t valueStart = startPos + 8; // after "data":"
        size_t valueEnd = s.find('\"', valueStart);
        if (valueEnd != std::string::npos && valueEnd - valueStart > 100) {
            s.replace(valueStart + 10, valueEnd - (valueStart + 10), "...<truncated>...");
            // adjust end position after replacement
            valueEnd = valueStart + 10 + 16; // "...<truncated>..." length
        }
        startPos = valueEnd + 1;
    }

    return s;
}

struct HostContext {
    WebViewHost webViewHost;
    AppWindow appWindow;
    void (*onMessage)(const char*) = nullptr;
};

static constexpr int kWindowWidthDips = 435;
static constexpr int kWindowDpiBase = 96;

extern "C" {

__declspec(dllexport) void __stdcall Host_Start(
    void (*onMessage)(const char*),
    void (*onResized)(int, int),
    void (*onReady)(void*))
{
    FILE* fp{};
    bool hasConsole = false;
    
    // Check if we need to enable debug mode console
    if (IsDebugModeEnabled()) {
        // Try to attach to parent terminal console first, if fails then allocate a standalone console
        if (AttachConsole(ATTACH_PARENT_PROCESS)) {
            hasConsole = true;
        } else if (AllocConsole()) {
            hasConsole = true;
        }
        
        if (hasConsole) {
            freopen_s(&fp, "CONOUT$", "w", stdout);
            freopen_s(&fp, "CONOUT$", "w", stderr);
            std::println("=================================================");
            std::println("--- Immersive Display Host DEBUG CONSOLE ---");
            std::println("=================================================");
        }
    } else {
        // Normal mode: run silently, redirecting standard output to null
        freopen_s(&fp, "nul", "w", stdout);
        freopen_s(&fp, "nul", "w", stderr);
    }
    (void)fp;

    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    auto* ctx = new HostContext();
    ctx->onMessage = onMessage;

    UINT dpi = GetDpiForSystem();
    int width = MulDiv(kWindowWidthDips, dpi, kWindowDpiBase);

    HINSTANCE hInstance = GetModuleHandleW(NULL);
    if (!ctx->appWindow.Create(hInstance, L"Immersive Display", width, width)) {
        delete ctx;
        CoUninitialize();
        if (hasConsole) FreeConsole();
        return;
    }

    // Reposition and resize window to fit the monitor work area
    // - Available height ≤ 850 DIPs → fill full height
    // - Available height > 850 DIPs → use 80%, cap at 1000 DIPs
    {
        HWND hWndAdj = ctx->appWindow.GetHwnd();
        HMONITOR hMon = MonitorFromWindow(hWndAdj, MONITOR_DEFAULTTONEAREST);
        MONITORINFO miAdj{};
        miAdj.cbSize = sizeof(miAdj);
        if (GetMonitorInfoW(hMon, &miAdj)) {
            RECT& work = miAdj.rcWork;
            int adjHeightPx = work.bottom - work.top;
            int adjHeightDips = MulDiv(adjHeightPx, kWindowDpiBase, dpi);

            int targetDips;
            if (adjHeightDips <= 850) {
                targetDips = adjHeightDips;
            } else {
                targetDips = (std::min)(adjHeightDips * 95 / 100, 1000);
            }

            int finalHeight = MulDiv(targetDips, dpi, kWindowDpiBase);
            int adjX = work.left + (work.right - work.left - width) / 2;
            int adjY = work.top;
            SetWindowPos(hWndAdj, NULL, adjX, adjY, width, finalHeight, SWP_NOZORDER | SWP_NOACTIVATE);
            std::println("[Host] Window adjusted: {} DIPs ({} px) @ ({},{})", targetDips, finalHeight, adjX, adjY);
        }
    }

    // Set window icon from WebUI/favicon.ico
    {
        std::wstring webUiPath = InteropHelper::GetWebUiPath();
        auto lastSep = webUiPath.find_last_of(L"\\/");
        if (lastSep != std::wstring::npos) {
            std::wstring iconPath = webUiPath.substr(0, lastSep) + L"\\favicon.ico";
            HICON hIcon = (HICON)LoadImageW(NULL, iconPath.c_str(), IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            if (hIcon) {
                SendMessageW(ctx->appWindow.GetHwnd(), WM_SETICON, ICON_BIG, (LPARAM)hIcon);
                SendMessageW(ctx->appWindow.GetHwnd(), WM_SETICON, ICON_SMALL, (LPARAM)hIcon);
                std::println("[Host] Window icon loaded: WebUI/favicon.ico");
            } else {
                std::println("[Host] favicon.ico not found, skipping custom icon.");
            }
        }
    }

    ctx->webViewHost.SetMessageCallback([ctx](const std::string& utf8Msg) {
        // Print the raw message sent from JS/C++ to C# (with base64 filtered)
        std::println("[C++ Host] Sent Message to C# (from JS): {}", FilterBase64(utf8Msg));
        
        if (ctx->onMessage) ctx->onMessage(utf8Msg.c_str());
    });

    ctx->appWindow.SetCustomMessageCallback([ctx](UINT msg, WPARAM wp, LPARAM lp) {
        if (msg == WM_EXECUTE_SCRIPT) {
            auto* jsonPtr = reinterpret_cast<std::wstring*>(lp);
            if (jsonPtr) {
                ctx->webViewHost.PostJsonMessage(*jsonPtr);
                delete jsonPtr;
            }
        }
    });

    ctx->appWindow.SetResizeCallback([ctx, onResized]() {
        ctx->webViewHost.Resize(ctx->appWindow.GetHwnd());
        if (ctx->appWindow.GetHwnd()) {
            RECT r{};
            GetClientRect(ctx->appWindow.GetHwnd(), &r);
            if (onResized) onResized(r.right - r.left, r.bottom - r.top);
        }
    });

    ctx->appWindow.SetDestroyCallback([]() {
        PostQuitMessage(0);
    });

    ctx->webViewHost.Initialize(ctx->appWindow.GetHwnd(), hasConsole, [ctx, onReady]() {
        std::println("[Host DLL] WebView2 initialized.");
        std::wstring indexPath = InteropHelper::GetWebUiPath();
        std::println("[Host DLL] Navigating to: {}", InteropHelper::WideToUtf8(indexPath.c_str()));
        ctx->webViewHost.Navigate(InteropHelper::PathToUri(indexPath));
        if (onReady) onReady(ctx);
    });

    ctx->appWindow.Run();

    delete ctx;
    CoUninitialize();
    if (hasConsole) FreeConsole();
}

__declspec(dllexport) void __stdcall Host_PostMessage(void* ctx, const char* jsonUtf8)
{
    if (!ctx || !jsonUtf8) return;

    std::println("[C++ Host] State Push -> JS: {}", FilterBase64(jsonUtf8));

    auto* host = static_cast<HostContext*>(ctx);
    auto* jsonPtr = new std::wstring(InteropHelper::Utf8ToWide(jsonUtf8));
    if (!PostMessageW(host->appWindow.GetHwnd(), WM_EXECUTE_SCRIPT, 0, reinterpret_cast<LPARAM>(jsonPtr))) {
        delete jsonPtr;
    }
}

__declspec(dllexport) void __stdcall Host_Shutdown(void* ctx)
{
    if (!ctx) return;
    PostMessageW(static_cast<HostContext*>(ctx)->appWindow.GetHwnd(), WM_CLOSE, 0, 0);
}

} // extern "C"

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID)
{
    return TRUE;
}
