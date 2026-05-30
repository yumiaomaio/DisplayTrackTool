using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Structs;
using ImmersiveDisplay.Services;

namespace ImmersiveDisplay.Engine;

/// <summary>
/// Dedicated STA thread with a Win32 message pump.
/// Other components dispatch work to this thread via Post().
/// All HWND operations must run here.
/// </summary>
public class WindowThread : IDisposable
{
    private const string ClassName = "ImmersiveWindowThread";
    private const uint WM_EXECUTE = NativeMethods.WM_USER + 0x1000;

    private Thread? _thread;
    private IntPtr _hwnd = IntPtr.Zero;
    private readonly ManualResetEventSlim _hwndReady = new();
    private readonly ConcurrentQueue<Action> _actionQueue = new();
    private readonly NativeMethods.WndProc _wndProcDelegate;
    private readonly ILoggingService _loggingService;

    public IntPtr Hwnd => _hwnd;

    public WindowThread(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _wndProcDelegate = WndProc;
    }

    public void Start()
    {
        if (_thread != null) return;

        _thread = new Thread(ThreadProc)
        {
            Name = "WindowThread",
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _hwndReady.Wait();
    }

    public void Stop()
    {
        if (_thread == null) return;

        var hwnd = _hwnd;
        _thread = null;

        if (hwnd != IntPtr.Zero)
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Queue an action to run on the UI thread.
    /// </summary>
    public void Post(Action action)
    {
        if (_hwnd == IntPtr.Zero) return;
        _actionQueue.Enqueue(action);
        NativeMethods.PostMessage(_hwnd, WM_EXECUTE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Queue an action and block until the WindowThread has executed it.
    /// Uses SendMessageW which blocks the calling thread until the
    /// WindowThread's WndProc drains the queue.
    /// </summary>
    public void Send(Action action)
    {
        if (_hwnd == IntPtr.Zero) return;
        _actionQueue.Enqueue(action);
        NativeMethods.SendMessage(_hwnd, WM_EXECUTE, IntPtr.Zero, IntPtr.Zero);
    }

    // --- Thread lifecycle ---

    private void ThreadProc()
    {
        RegisterWindowClass();

        _hwnd = NativeMethods.CreateWindowEx(
            0, ClassName, "WindowThreadMsg",
            0, 0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null!),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            _loggingService.AddLog("[WindowThread] Failed to create message window.");
            return;
        }

        _hwndReady.Set();

        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(in msg);
            NativeMethods.DispatchMessage(in msg);
        }

        _hwnd = IntPtr.Zero;
    }

    private void RegisterWindowClass()
    {
        var wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = NativeMethods.GetModuleHandle(null!),
            lpszClassName = Marshal.StringToHGlobalUni(ClassName)
        };

        try
        {
            NativeMethods.RegisterClassEx(in wndClass);
        }
        finally
        {
            Marshal.FreeHGlobal(wndClass.lpszClassName);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_EXECUTE:
                ExecutePendingActions();
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ExecutePendingActions()
    {
        while (_actionQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _loggingService.AddLog($"[WindowThread] Execute error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
