#include "WebViewHost.h"
#include "InteropHelper.h"
#include <print>
#include <wrl.h>

using namespace Microsoft::WRL;

namespace Immersive {

// Helper awaiter for WebView2 async operations (Safely stores data in coroutine frame)
template<typename TInterface>
struct WebView2Awaiter {
    std::function<HRESULT(std::function<void(HRESULT, TInterface*)>)> m_starter;
    HRESULT m_hr = S_OK;
    ComPtr<TInterface> m_result;

    bool await_ready() const { return false; }
    void await_suspend(std::coroutine_handle<> handle) {
        m_starter([this, handle](HRESULT hr, TInterface* res) {
            m_hr = hr;
            m_result = res;
            handle.resume();
        });
    }
    std::pair<HRESULT, ComPtr<TInterface>> await_resume() { return { m_hr, m_result }; }
};

WebViewHost::WebViewHost() : m_firstNavigationPerformed(false) {}
WebViewHost::~WebViewHost() {}

AsyncVoid WebViewHost::InitializeAsync(HWND parentHwnd, std::function<void()> onReady) {
    m_firstNavigationPerformed = false;

    // 1. Await Environment Creation
    auto [hrEnv, env] = co_await WebView2Awaiter<ICoreWebView2Environment>{
        [](auto cb) { 
            return CreateCoreWebView2EnvironmentWithOptions(nullptr, nullptr, nullptr, 
                Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
                    [cb](HRESULT hr, ICoreWebView2Environment* e) { cb(hr, e); return S_OK; }).Get()); 
        }
    };

    if (FAILED(hrEnv)) {
        std::println("[C++] Environment creation failed: 0x{:08X}", (unsigned int)hrEnv);
        co_return;
    }

    // 2. Await Controller Creation
    auto [hrCtrl, controller] = co_await WebView2Awaiter<ICoreWebView2Controller>{
        [env, parentHwnd](auto cb) { 
            return env->CreateCoreWebView2Controller(parentHwnd, 
                Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                    [cb](HRESULT hr, ICoreWebView2Controller* c) { cb(hr, c); return S_OK; }).Get()); 
        }
    };

    if (FAILED(hrCtrl)) {
        std::println("[C++] Controller creation failed: 0x{:08X}", (unsigned int)hrCtrl);
        co_return;
    }

    m_controller = controller;
    m_controller->get_CoreWebView2(&m_webView);

    // Initial size & Settings
    Resize(parentHwnd);
    ComPtr<ICoreWebView2Settings> settings;
    m_webView->get_Settings(&settings);
    settings->put_IsScriptEnabled(TRUE);
    settings->put_IsWebMessageEnabled(TRUE);
    
    // Security & UI Lockdown
    settings->put_AreDefaultContextMenusEnabled(FALSE); 
    settings->put_AreDevToolsEnabled(FALSE);            
    settings->put_IsZoomControlEnabled(FALSE);          
    settings->put_IsStatusBarEnabled(FALSE);            

    // --- Navigation Interception (拦截所有导航) ---
    m_webView->add_NavigationStarting(Callback<ICoreWebView2NavigationStartingEventHandler>(
        [this](ICoreWebView2* sender, ICoreWebView2NavigationStartingEventArgs* args) -> HRESULT {
            LPWSTR uri = nullptr;
            if (SUCCEEDED(args->get_Uri(&uri)) && uri) {
                std::wstring uriStr(uri);
                if (m_firstNavigationPerformed) {
                    std::println("[C++ Nav] Intercepted Navigation: {}", InteropHelper::WideToUtf8(uri));
                    args->put_Cancel(TRUE); // 阻止当前窗口跳转
                    if (m_messageCallback) {
                        std::string json = std::format(R"({{"action":"HandleAppProtocol","payload":"{}"}})", InteropHelper::WideToUtf8(uri));
                        m_messageCallback(json);
                    }
                } else {
                    m_firstNavigationPerformed = true;
                }
                CoTaskMemFree(uri);
            }
            return S_OK;
        }).Get(), nullptr);

    // --- New Window Interception (拦截弹出新窗口) ---
    m_webView->add_NewWindowRequested(Callback<ICoreWebView2NewWindowRequestedEventHandler>(
        [this](ICoreWebView2* sender, ICoreWebView2NewWindowRequestedEventArgs* args) -> HRESULT {
            LPWSTR uri = nullptr;
            if (SUCCEEDED(args->get_Uri(&uri)) && uri) {
                std::println("[C++ Nav] Intercepted New Window Request: {}", InteropHelper::WideToUtf8(uri));

                // 1. 核心：标记为已处理，阻止弹出独立窗口
                args->put_Handled(TRUE); 

                // 2. 透传给 C# 处理逻辑
                if (m_messageCallback) {
                    std::string json = std::format(R"({{"action":"HandleAppProtocol","payload":"{}"}})", InteropHelper::WideToUtf8(uri));
                    m_messageCallback(json);
                }
                CoTaskMemFree(uri);
            }
            return S_OK;
        }).Get(), nullptr);

    // --- Download Interception (拦截下载行为) ---
    ComPtr<ICoreWebView2_4> webView4;
    if (SUCCEEDED(m_webView.As(&webView4))) {
        webView4->add_DownloadStarting(Callback<ICoreWebView2DownloadStartingEventHandler>(
            [this](ICoreWebView2* sender, ICoreWebView2DownloadStartingEventArgs* args) -> HRESULT {
                ComPtr<ICoreWebView2DownloadOperation> download;
                args->get_DownloadOperation(&download);

                LPWSTR uri = nullptr;
                if (download && SUCCEEDED(download->get_Uri(&uri)) && uri) {
                    std::println("[C++ Nav] Intercepted Download: {}", InteropHelper::WideToUtf8(uri));

                    // 1. 核心：取消下载任务
                    args->put_Cancel(TRUE);
                    // 2. 隐藏默认下载 UI
                    args->put_Handled(TRUE);

                    // 3. 透传给 C#
                    if (m_messageCallback) {
                        std::string json = std::format(R"({{"action":"HandleAppProtocol","payload":"{}"}})", InteropHelper::WideToUtf8(uri));
                        m_messageCallback(json);
                    }
                    CoTaskMemFree(uri);
                }
                return S_OK;
            }).Get(), nullptr);
    }


    // Resource Interception
    m_webView->AddWebResourceRequestedFilter(L"*", COREWEBVIEW2_WEB_RESOURCE_CONTEXT_ALL);
    m_webView->add_WebResourceRequested(Callback<ICoreWebView2WebResourceRequestedEventHandler>(
        [this](ICoreWebView2* sender, ICoreWebView2WebResourceRequestedEventArgs* args) -> HRESULT {
            ComPtr<ICoreWebView2WebResourceRequest> request;
            args->get_Request(&request);

            LPWSTR uri = nullptr;
            if (SUCCEEDED(request->get_Uri(&uri)) && uri) {
                // Low-level resource logging
                if (std::wstring_view(uri).find(L"file://") == 0) {
                    // Optional: log or handle specific file resources
                }
                CoTaskMemFree(uri);
            }
            return S_OK;
        }).Get(), nullptr);

    // Message Handling
    m_webView->add_WebMessageReceived(Callback<ICoreWebView2WebMessageReceivedEventHandler>(
        [this](ICoreWebView2* webview, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
            // 1. Try to handle files from AdditionalObjects (Drag & Drop support)
            ComPtr<ICoreWebView2WebMessageReceivedEventArgs2> args2;
            if (SUCCEEDED(args->QueryInterface(IID_PPV_ARGS(&args2)))) {
                ComPtr<ICoreWebView2ObjectCollectionView> objects;
                if (SUCCEEDED(args2->get_AdditionalObjects(&objects)) && objects) {
                    unsigned int count = 0;
                    objects->get_Count(&count);
                    for (unsigned int i = 0; i < count; i++) {
                        ComPtr<IUnknown> obj;
                        if (SUCCEEDED(objects->GetValueAtIndex(i, &obj))) {
                            ComPtr<ICoreWebView2File> file;
                            if (SUCCEEDED(obj->QueryInterface(IID_PPV_ARGS(&file)))) {
                                LPWSTR path = nullptr;
                                if (SUCCEEDED(file->get_Path(&path)) && path) {
                                    std::println("[C++ Drop] File dropped: {}", InteropHelper::WideToUtf8(path));
                                    
                                    // Forward to C# as a protocol command
                                    if (m_messageCallback) {
                                        std::string json = std::format(R"({{"action":"HandleAppProtocol","payload":"{}"}})", InteropHelper::WideToUtf8(path));
                                        m_messageCallback(json);
                                    }
                                    CoTaskMemFree(path);
                                    
                                    // Normally we only process the first file for this tool
                                    return S_OK;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Standard Message Handling (JSON/String)
            LPWSTR message = nullptr;
            if (SUCCEEDED(args->TryGetWebMessageAsString(&message)) && message) {
                if (m_messageCallback) m_messageCallback(InteropHelper::WideToUtf8(message));
                CoTaskMemFree(message);
            } else {
                args->get_WebMessageAsJson(&message);
                if (message) {
                    if (m_messageCallback) m_messageCallback(InteropHelper::WideToUtf8(message));
                    CoTaskMemFree(message);
                }
            }
            return S_OK;
        }).Get(), nullptr);

    if (onReady) onReady();
    co_return;
}

HRESULT WebViewHost::Initialize(HWND parentHwnd, std::function<void()> onReady) {
    InitializeAsync(parentHwnd, onReady);
    return S_OK;
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

} // namespace Immersive
