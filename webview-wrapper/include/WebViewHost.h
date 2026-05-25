#pragma once
#include <windows.h>
#include <wrl.h>
#include "WebView2.h"
#include "CoroTask.h"
#include <string>
#include <functional>

namespace Immersive {

class WebViewHost {
public:
    WebViewHost();
    virtual ~WebViewHost();

    // Initialize using C++20 Coroutines (Fire and Forget)
    AsyncVoid InitializeAsync(HWND parentHwnd, bool debugMode, std::function<void()> onReady);

    // Entry point from main
    HRESULT Initialize(HWND parentHwnd, bool debugMode, std::function<void()> onReady);

    // Navigate to a URL
    HRESULT Navigate(const std::wstring& url);

    // Execute JavaScript in the WebView
    HRESULT ExecuteScript(const std::wstring& script);

    // Post a JSON message to the web content (thread-safe, marshals to STA internally)
    HRESULT PostJsonMessage(const std::wstring& json);

    // Resize the WebView to match the parent window's client area
    void Resize(HWND parentHwnd);

    // Set the callback for WebMessageReceived
    void SetMessageCallback(std::function<void(const std::string&)> callback);

private:
    Microsoft::WRL::ComPtr<ICoreWebView2Controller> m_controller;
    Microsoft::WRL::ComPtr<ICoreWebView2> m_webView;
    std::function<void(const std::string&)> m_messageCallback;
    bool m_firstNavigationPerformed = false;
};

} // namespace Immersive
