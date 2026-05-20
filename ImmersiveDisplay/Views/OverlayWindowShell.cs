// File: Views/OverlayWindowShell.cs

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Views;

public class OverlayWindowShell : IDisposable
{
    private static bool _classRegistered = false;
    private const string ClassName = "ImmersiveOverlayWindow";
    
    [ThreadStatic]
    private static OverlayWindowShell? _creatingInstance;
    private static readonly NativeMethods.WndProc _staticWndProcDelegate = StaticWndProc;
    
    private IntPtr _hwnd = IntPtr.Zero;
    private readonly string? _imagePath;
    private readonly uint _colorRef;
    private Bitmap? _cachedBitmap;

    public IntPtr Hwnd => _hwnd;

    public OverlayWindowShell(string? imagePath, string backgroundColor)
    {
        _imagePath = imagePath;
        _colorRef = ParseColorToColorRef(backgroundColor);

        if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
        {
            try
            {
                _cachedBitmap = new Bitmap(_imagePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OverlayWindowShell] Failed to load image {_imagePath}: {ex.Message}");
            }
        }

        EnsureClassRegistered();
    }

    private void EnsureClassRegistered()
    {
        if (_classRegistered) return;

        var wndClass = new NativeMethods.WNDCLASSEX();
        wndClass.cbSize = (uint)Marshal.SizeOf(wndClass);
        wndClass.style = 0;
        wndClass.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_staticWndProcDelegate);
        wndClass.cbClsExtra = 0;
        wndClass.cbWndExtra = 0;
        wndClass.hInstance = NativeMethods.GetModuleHandle(null!);
        wndClass.hIcon = IntPtr.Zero;
        wndClass.hCursor = IntPtr.Zero;
        wndClass.hbrBackground = IntPtr.Zero; // Clear background to prevent flashes
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
                throw new Exception($"Failed to register OverlayWindow class. Error: {error}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }

        _classRegistered = true;
    }

    public void Create(int x, int y, int width, int height)
    {
        uint dwExStyle = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_NOACTIVATE;
        uint dwStyle = NativeMethods.WS_POPUP; // Create invisible first to prevent activation issues during CreateWindowEx

        _creatingInstance = this;
        _hwnd = NativeMethods.CreateWindowEx(
            dwExStyle,
            ClassName,
            "Overlay Cover",
            dwStyle,
            x,
            y,
            width,
            height,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null!),
            IntPtr.Zero);

        _creatingInstance = null; // Reset thread-static field

        if (_hwnd == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"Failed to create OverlayWindow. Error: {error}");
        }
    }

    public void Show()
    {
        if (_hwnd != IntPtr.Zero)
        {
            // Use SW_SHOWNOACTIVATE to prevent taking input focus and causing focus struggle loops
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);
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
        OverlayWindowShell? instance = null;
        IntPtr userData = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWLP_USERDATA);
        
        if (userData != IntPtr.Zero)
        {
            GCHandle gcHandle = GCHandle.FromIntPtr(userData);
            instance = (OverlayWindowShell?)gcHandle.Target;
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
            case NativeMethods.WM_ERASEBKGND:
                return (IntPtr)1; // Handled

            case NativeMethods.WM_PAINT:
                {
                    IntPtr hdc = NativeMethods.BeginPaint(hWnd, out var ps);
                    try
                    {
                        NativeMethods.GetClientRect(hWnd, out var clientRect);

                        if (_cachedBitmap != null)
                        {
                            using (var graphics = Graphics.FromHdc(hdc))
                            {
                                graphics.DrawImage(_cachedBitmap, 0, 0, clientRect.Width, clientRect.Height);
                            }
                        }
                        else
                        {
                            IntPtr brush = NativeMethods.CreateSolidBrush(_colorRef);
                            NativeMethods.FillRect(hdc, in clientRect, brush);
                            NativeMethods.DeleteObject(brush);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OverlayWindowShell] Draw Error: {ex.Message}");
                    }
                    finally
                    {
                        NativeMethods.EndPaint(hWnd, in ps);
                    }
                }
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static uint ParseColorToColorRef(string hexColor)
    {
        try
        {
            hexColor = hexColor.TrimStart('#');
            if (hexColor.Length == 8)
            {
                hexColor = hexColor.Substring(2); // Trim alpha
            }
            if (hexColor.Length == 6)
            {
                byte r = Convert.ToByte(hexColor.Substring(0, 2), 16);
                byte g = Convert.ToByte(hexColor.Substring(2, 2), 16);
                byte b = Convert.ToByte(hexColor.Substring(4, 2), 16);
                return r | ((uint)g << 8) | ((uint)b << 16); // BBGGRR format
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OverlayWindowShell] Error parsing color '{hexColor}': {ex.Message}");
        }
        return 0; // Default to black
    }

    public void Dispose()
    {
        Close();
        if (_cachedBitmap != null)
        {
            _cachedBitmap.Dispose();
            _cachedBitmap = null;
        }
    }
}
