#include "AppWindow.h"

namespace Immersive {

AppWindow::AppWindow() : m_hWnd(NULL) {}
AppWindow::~AppWindow() {}

bool AppWindow::Create(HINSTANCE hInstance, const std::wstring& title, int width, int height) {
    const wchar_t szWindowClass[] = L"ImmersiveHostWindow";

    WNDCLASSEXW wcex = {
        .cbSize = sizeof(WNDCLASSEXW),
        .style = CS_HREDRAW | CS_VREDRAW,
        .lpfnWndProc = StaticWndProc,
        .hInstance = hInstance,
        .hCursor = LoadCursor(NULL, IDC_ARROW),
        .hbrBackground = (HBRUSH)(COLOR_WINDOW + 1),
        .lpszClassName = szWindowClass
    };
    RegisterClassExW(&wcex);

    m_hWnd = CreateWindowExW(0, szWindowClass, title.c_str(), WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, width, height, NULL, NULL, hInstance, this);

    if (!m_hWnd) return false;

    ShowWindow(m_hWnd, SW_SHOW);
    UpdateWindow(m_hWnd);

    return true;
}

int AppWindow::Run() {
    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    return (int)msg.wParam;
}

void AppWindow::SetResizeCallback(std::function<void()> callback) {
    m_resizeCallback = callback;
}

void AppWindow::SetDestroyCallback(std::function<void()> callback) {
    m_destroyCallback = callback;
}

LRESULT CALLBACK AppWindow::StaticWndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) {
    AppWindow* pThis = nullptr;
    if (message == WM_NCCREATE) {
        CREATESTRUCT* pCreate = (CREATESTRUCT*)lParam;
        pThis = (AppWindow*)pCreate->lpCreateParams;
        SetWindowLongPtr(hWnd, GWLP_USERDATA, (LONG_PTR)pThis);
    } else {
        pThis = (AppWindow*)GetWindowLongPtr(hWnd, GWLP_USERDATA);
    }

    if (pThis) {
        return pThis->WndProc(hWnd, message, wParam, lParam);
    }
    return DefWindowProc(hWnd, message, wParam, lParam);
}

LRESULT CALLBACK AppWindow::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) {
    switch (message) {
    case WM_SIZE:
        if (m_resizeCallback) m_resizeCallback();
        break;
    case WM_DESTROY:
        if (m_destroyCallback) m_destroyCallback();
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

} // namespace Immersive
