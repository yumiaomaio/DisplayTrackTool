#pragma once
#include <windows.h>
#include <functional>
#include <string>

namespace Immersive {

class AppWindow {
public:
    AppWindow();
    ~AppWindow();

    // Create the main application window
    bool Create(HINSTANCE hInstance, const std::wstring& title, int width, int height);

    // Run the application message loop
    int Run();

    // Get the window handle
    HWND GetHwnd() const { return m_hWnd; }

    // Set a resize callback
    void SetResizeCallback(std::function<void()> callback);

    // Set a destroy callback
    void SetDestroyCallback(std::function<void()> callback);

private:
    static LRESULT CALLBACK StaticWndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);
    LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);

    HWND m_hWnd;
    std::function<void()> m_resizeCallback;
    std::function<void()> m_destroyCallback;
};

} // namespace Immersive
