#pragma once
#include <windows.h>
#include <wrl.h>
#include "WebView2.h"
#include <string>
#include <functional>

namespace Immersive {

class WebViewHost {
public:
    WebViewHost();
    ~WebViewHost();

    // Initialize the WebView2 environment and controller
    HRESULT Initialize(HWND parentHwnd, std::function<void()> onReady);

    // Navigate to a URL
    HRESULT Navigate(const std::wstring& url);

    // Execute JavaScript in the WebView
    HRESULT ExecuteScript(const std::wstring& script);

    // Resize the WebView to match the parent window's client area
    void Resize(HWND parentHwnd);

    // Set the callback for WebMessageReceived
    void SetMessageCallback(std::function<void(const std::string&)> callback);

private:
    Microsoft::WRL::ComPtr<ICoreWebView2Controller> m_controller;
    Microsoft::WRL::ComPtr<ICoreWebView2> m_webView;
    std::function<void(const std::string&)> m_messageCallback;

    std::wstring GetShimScript();
};

} // namespace Immersive
