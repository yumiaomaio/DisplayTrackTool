#include "WebViewHost.h"
#include "InteropHelper.h"
#include <stdio.h>

using namespace Microsoft::WRL;

namespace Immersive {

WebViewHost::WebViewHost() {}
WebViewHost::~WebViewHost() {}

HRESULT WebViewHost::Initialize(HWND parentHwnd, std::function<void()> onReady) {
    return CreateCoreWebView2EnvironmentWithOptions(nullptr, nullptr, nullptr,
        Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
            [this, parentHwnd, onReady](HRESULT result, ICoreWebView2Environment* env) -> HRESULT {
                if (FAILED(result)) {
                    printf("[C++] ERROR: CreateCoreWebView2EnvironmentWithOptions failed with HRESULT 0x%08X\n", result);
                    return result;
                }

                env->CreateCoreWebView2Controller(parentHwnd,
                    Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                        [this, parentHwnd, onReady](HRESULT result, ICoreWebView2Controller* controller) -> HRESULT {
                            if (FAILED(result) || controller == nullptr) {
                                printf("[C++] ERROR: CreateCoreWebView2Controller failed with HRESULT 0x%08X\n", result);
                                return result;
                            }

                            m_controller = controller;
                            m_controller->get_CoreWebView2(&m_webView);

                            // Initial size
                            Resize(parentHwnd);

                            // Settings
                            ComPtr<ICoreWebView2Settings> settings;
                            m_webView->get_Settings(&settings);
                            settings->put_IsScriptEnabled(TRUE);
                            settings->put_IsWebMessageEnabled(TRUE);
                            settings->put_AreDefaultContextMenusEnabled(TRUE);

                            // Inject Shim (Restored)
                            std::wstring shim = GetShimScript();
                            m_webView->AddScriptToExecuteOnDocumentCreated(shim.c_str(), nullptr);

                            // Message Handling (Transparent Relay with Debugging)
                            m_webView->add_WebMessageReceived(Callback<ICoreWebView2WebMessageReceivedEventHandler>(
                                [this](ICoreWebView2* webview, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
                                    LPWSTR message = nullptr;
                                    HRESULT hr = args->TryGetWebMessageAsString(&message);
                                    if (FAILED(hr) || message == nullptr) {
                                        args->get_WebMessageAsJson(&message);
                                    }

                                    if (message != nullptr) {
                                        std::string utf8Msg = InteropHelper::WideToUtf8(message);
                                        if (m_messageCallback) {
                                            m_messageCallback(utf8Msg);
                                        }
                                        CoTaskMemFree(message);
                                    }
                                    return S_OK;
                                }).Get(), nullptr);

                            if (onReady) onReady();
                            return S_OK;
                        }).Get());
                return S_OK;
            }).Get());
}

HRESULT WebViewHost::Navigate(const std::wstring& url) {
    if (!m_webView) return E_POINTER;
    return m_webView->Navigate(url.c_str());
}

HRESULT WebViewHost::ExecuteScript(const std::wstring& script) {
    if (!m_webView) return E_POINTER;
    return m_webView->ExecuteScript(script.c_str(), nullptr);
}

void WebViewHost::Resize(HWND parentHwnd) {
    if (m_controller) {
        RECT bounds;
        GetClientRect(parentHwnd, &bounds);
        m_controller->put_Bounds(bounds);
    }
}

void WebViewHost::SetMessageCallback(std::function<void(const std::string&)> callback) {
    m_messageCallback = callback;
}

std::wstring WebViewHost::GetShimScript() {
    // Robust Proxy Shim that maps C# PascalCase to JS camelCase and handles Async Methods
    return L"(function() {"
           L"  const state = {};"
           L"  const pendingCalls = new Map();"
           L"  const methods = ["
           L"    'StartMonitoring', 'StopMonitoring', 'SetBackgroundColor', 'SetTargetProcessName', "
           L"    'SetAssociatedLaunchPath', 'SetEnableTaskbarAutoHide', 'SetEnableDisplaySync', "
           L"    'SetEnableBackgroundOverlay', 'SetBackgroundMode', 'SelectImage', 'ClearImage', "
           L"    'SelectAssociatedProgram', 'SetLaunchOnAppStartup', 'SetLaunchOnTaskStart', "
           L"    'SetAutoStartFromThirdParty', 'SetAutoStartMonitoringOnProtocolLaunch', "
           L"    'SetWindowDetectionTimeout', 'RegisterProtocol', 'UnregisterProtocol', "
           L"    'CleanAssociation', 'ClearLogs', 'SaveConfig', 'RestartAsAdmin', 'ExitApp', 'ShowAbout', "
           L"    'GetProcessCommandLine', 'CheckProcessExists', 'GetProcessIconBase64', 'GetImageBase64', 'GetLogs'"
           L"  ];"
           L"  window.chrome = window.chrome || {};"
           L"  window.chrome.webview = window.chrome.webview || {};"
           L"  window.chrome.webview.hostObjects = {"
           L"    bridge: new Proxy(state, {"
           L"      get: function(target, prop) {"
           L"        if (typeof prop !== 'string' || prop === 'then') return target[prop];"
           L"        if (methods.includes(prop)) {"
           L"          return function(...args) {"
           L"            return new Promise((resolve) => {"
           L"              const callId = Math.random().toString(36).substring(7);"
           L"              pendingCalls.set(callId, resolve);"
           L"              window.chrome.webview.postMessage({ action: prop, payload: args[0], callId: callId });"
           L"            });"
           L"          };"
           L"        }"
           L"        const camelProp = prop.charAt(0).toLowerCase() + prop.slice(1);"
           L"        return target.hasOwnProperty(camelProp) ? target[camelProp] : target[prop];"
           L"      }"
           L"    })"
           L"  };"
           L"  window.onStateChangedFromDll = function(data) {"
           L"    if (!data) return;"
           L"    if (data.callId && pendingCalls.has(data.callId)) {"
           L"       const resolve = pendingCalls.get(data.callId);"
           L"       pendingCalls.delete(data.callId);"
           L"       resolve(data.result);"
           L"    }"
           L"    let stateData = (data.status === 'ok' && data.hasOwnProperty('result')) ? data.result : data;"
           L"    if (stateData && typeof stateData === 'object') {"
           L"       Object.assign(state, stateData);"
           L"       if (window.onStateChanged) window.onStateChanged(stateData);"
           L"    }"
           L"  };"
           L"  window.chrome.webview.postMessage({ action: 'GetInitialState' });"
           L"})();";
}

} // namespace Immersive
