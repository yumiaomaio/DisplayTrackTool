// File: Views/MainWindowShell.cs

using System.Drawing;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Services;
using Microsoft.Web.WebView2.Core;

namespace ImmersiveDisplay.Views;

public class MainWindowShell
{
    private static bool _classRegistered = false;
    private const string ClassName = "ImmersiveMainWindow";
    private const int DefaultWidth = 435;
    private const int DefaultHeight = 800;

    [ThreadStatic]
    private static MainWindowShell? _creatingInstance;
    private static readonly NativeMethods.WndProc StaticWndProcDelegate = StaticWndProc;

    private IntPtr _hwnd = IntPtr.Zero;
    private readonly AppBridge _bridge;
    private readonly IAppIntegrationService _appIntegrationService;
    private readonly IConfigService _configService;
    private readonly ILoggingService _loggingService;
    private readonly Task<CoreWebView2Environment> _envTask;

    private CoreWebView2Controller? _webViewController;

    public IntPtr Hwnd => _hwnd;

    public MainWindowShell(
        AppBridge bridge,
        IAppIntegrationService appIntegrationService,
        IConfigService configService,
        ILoggingService loggingService,
        Task<CoreWebView2Environment> envTask)
    {
        _bridge = bridge;
        _appIntegrationService = appIntegrationService;
        _configService = configService;
        _loggingService = loggingService;
        _envTask = envTask;

        EnsureClassRegistered();
    }

    private void EnsureClassRegistered()
    {
        if (_classRegistered) return;

        var wndClass = new NativeMethods.WNDCLASSEX();
        wndClass.cbSize = (uint)Marshal.SizeOf(wndClass);
        wndClass.style = 0;
        wndClass.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(StaticWndProcDelegate);
        wndClass.cbClsExtra = 0;
        wndClass.cbWndExtra = 0;
        wndClass.hInstance = NativeMethods.GetModuleHandle(null!);
        wndClass.hIcon = IntPtr.Zero;
        wndClass.hCursor = IntPtr.Zero;
        wndClass.hbrBackground = (IntPtr)6; // COLOR_WINDOW + 1
        wndClass.lpszMenuName = IntPtr.Zero;
        
        IntPtr classNamePtr = Marshal.StringToHGlobalUni(ClassName);
        try
        {
            wndClass.lpszClassName = classNamePtr;
            wndClass.hIconSm = IntPtr.Zero;

            ushort regResult = NativeMethods.RegisterClassEx(in wndClass);
            if (regResult == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to register MainWindow class. Error: {error}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }

        _classRegistered = true;
    }

    public void Create()
    {
        uint dwExStyle = NativeMethods.WS_EX_APPWINDOW;
        uint dwStyle = NativeMethods.WS_OVERLAPPEDWINDOW | NativeMethods.WS_VISIBLE;

        // Scale initial width and height by system DPI factor
        uint dpi = 96;
        try
        {
            dpi = NativeMethods.GetDpiForSystem();
        }
        catch
        {
            // Graceful fallback if GetDpiForSystem is not supported
        }
        double dpiFactor = dpi / 96.0;
        int width = (int)(DefaultWidth * dpiFactor);
        int height = (int)(DefaultHeight * dpiFactor);

        _creatingInstance = this;
        _hwnd = NativeMethods.CreateWindowEx(
            dwExStyle,
            ClassName,
            "Immersive Display Track Tool",
            dwStyle,
            unchecked((int)0x80000000), // CW_USEDEFAULT
            unchecked((int)0x80000000),
            width,
            height,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null!),
            IntPtr.Zero);

        _creatingInstance = null;

        if (_hwnd == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"Failed to create MainWindow. Error: {error}");
        }

        UiDispatcher.Initialize(_hwnd, _loggingService);

        _ = InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var env = await _envTask;
            
            _webViewController = await env.CreateCoreWebView2ControllerAsync(_hwnd);

            NativeMethods.GetClientRect(_hwnd, out var clientRect);
            _webViewController.Bounds = new Rectangle(0, 0, clientRect.Width, clientRect.Height);

            _webViewController.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _webViewController.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            _webViewController.CoreWebView2.NavigationStarting += (s, ev) =>
            {
                if (!ev.Uri.Contains("/WebUI/", StringComparison.OrdinalIgnoreCase))
                {
                    ev.Cancel = true;
                    HandleExternalNavigation(ev.Uri);
                }
            };

            _webViewController.CoreWebView2.NewWindowRequested += (s, ev) =>
            {
                if (!ev.Uri.Contains("/WebUI/", StringComparison.OrdinalIgnoreCase))
                {
                    ev.Handled = true;
                    HandleExternalNavigation(ev.Uri);
                }
            };

            _bridge.Initialize(_webViewController.CoreWebView2);
            _webViewController.CoreWebView2.AddHostObjectToScript("bridge", _bridge);

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebUI", "index.html");
            if (File.Exists(htmlPath))
            {
                _webViewController.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            }
            else
            {
                _loggingService.AddLog($"Web UI file not found at: {htmlPath}");
            }

            _appIntegrationService.InitializeHooksAndTriggers();
            _appIntegrationService.ExecuteStartupLogic();
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"WebView2 Initialization failed: {ex.Message}");
        }
    }

    private void HandleExternalNavigation(string uri)
    {
        try
        {
            string target = uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(uri).LocalPath
                : uri;
            
            HandleFileDrop(target);
        }
        catch (Exception ex)
        {
            _loggingService.AddLog($"[DragDrop] Error handling navigation: {ex.Message}");
        }
    }

    private void HandleFileDrop(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        
        string extension = Path.GetExtension(path).ToLower();
        string targetPath;

        if (extension == ".lnk")
        {
            targetPath = ShortcutResolver.Resolve(path);
        }
        else if (extension == ".exe")
        {
            targetPath = path.Contains(' ') ? $"\"{path}\"" : path;
        }
        else
        {
            targetPath = path;
        }

        _configService.SetAssociatedLaunchPath(targetPath);
    }

    public void Show()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(_hwnd, 5); // SW_SHOW
        }
    }

    public void Close()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        MainWindowShell? instance = null;
        IntPtr userData = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
        
        if (userData != IntPtr.Zero)
        {
            GCHandle gcHandle = GCHandle.FromIntPtr(userData);
            instance = (MainWindowShell?)gcHandle.Target;
        }
        else if (_creatingInstance != null)
        {
            instance = _creatingInstance;
            if (msg == NativeMethods.WM_NCCREATE || msg == NativeMethods.WM_CREATE)
            {
                GCHandle gcHandle = GCHandle.Alloc(instance);
                NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, GCHandle.ToIntPtr(gcHandle));
            }
        }

        if (msg == NativeMethods.WM_DESTROY)
        {
            userData = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
            if (userData != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(userData);
                if (gcHandle.IsAllocated)
                {
                    gcHandle.Free();
                }
                NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
            }
        }

        if (instance != null)
        {
            return instance.WndProc(hWnd, msg, wParam, lParam);
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_SIZE:
                if (_webViewController != null)
                {
                    NativeMethods.GetClientRect(hWnd, out var clientRect);
                    _webViewController.Bounds = new Rectangle(0, 0, clientRect.Width, clientRect.Height);
                }
                return IntPtr.Zero;

            case UiDispatcher.WM_DISPATCH:
                UiDispatcher.InvokePending();
                return IntPtr.Zero;

            case NativeMethods.WM_CLOSE:
                Close();
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                _bridge.Dispose();
                if (_webViewController != null)
                {
                    _webViewController.CoreWebView2.Stop();
                    _webViewController.Close();
                    _webViewController = null;
                }
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
