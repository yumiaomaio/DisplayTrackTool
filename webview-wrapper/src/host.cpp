#include <windows.h>
#include <print>
#include <format>
#include <io.h>
#include <fcntl.h>
#include "AppWindow.h"
#include "WebViewHost.h"
#include "InteropHelper.h"

#define WM_EXECUTE_SCRIPT (WM_USER + 1)

using namespace Immersive;

struct HostContext {
    WebViewHost webViewHost;
    AppWindow appWindow;
    void (*onMessage)(const char*) = nullptr;
};

static constexpr int kWindowWidthDips = 435;
static constexpr int kWindowHeightDips = 850;
static constexpr int kWindowDpiBase = 96;

extern "C" {

__declspec(dllexport) void __stdcall Host_Start(
    void (*onMessage)(const char*),
    void (*onResized)(int, int),
    void (*onReady)(void*))
{
    bool hasConsole = AttachConsole(ATTACH_PARENT_PROCESS);
    FILE* fp{};
    if (hasConsole) {
        freopen_s(&fp, "CONOUT$", "w", stdout);
        freopen_s(&fp, "CONOUT$", "w", stderr);
        std::println("--- Immersive Display Host DLL ---");
    } else {
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
    int height = MulDiv(kWindowHeightDips, dpi, kWindowDpiBase);

    HINSTANCE hInstance = GetModuleHandleW(NULL);
    if (!ctx->appWindow.Create(hInstance, L"Immersive Display", width, height)) {
        delete ctx;
        CoUninitialize();
        if (hasConsole) FreeConsole();
        return;
    }

    ctx->webViewHost.SetMessageCallback([ctx](const std::string& utf8Msg) {
        if (ctx->onMessage) ctx->onMessage(utf8Msg.c_str());
    });

    ctx->appWindow.SetCustomMessageCallback([ctx](UINT msg, WPARAM wp, LPARAM lp) {
        if (msg == WM_EXECUTE_SCRIPT) {
            auto* scriptPtr = reinterpret_cast<std::wstring*>(lp);
            if (scriptPtr) {
                ctx->webViewHost.ExecuteScript(*scriptPtr);
                delete scriptPtr;
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

    ctx->webViewHost.Initialize(ctx->appWindow.GetHwnd(), [ctx, onReady]() {
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
    
    // Print the raw JSON received from C# to the C++ console
    std::println("[C++ Host] Received State Push from C#: {}", jsonUtf8);

    auto* host = static_cast<HostContext*>(ctx);
    std::wstring jsonWide = InteropHelper::Utf8ToWide(jsonUtf8);
    if (!jsonWide.empty()) {
        std::wstring script = std::format(
            L"if(window.onStateChangedFromDll) {{ window.onStateChangedFromDll({}); }}",
            jsonWide);
        
        auto* scriptPtr = new std::wstring(script);
        if (!PostMessageW(host->appWindow.GetHwnd(), WM_EXECUTE_SCRIPT, 0, reinterpret_cast<LPARAM>(scriptPtr))) {
            delete scriptPtr;
        }
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
