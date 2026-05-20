// File: Helpers/UiDispatcher.cs

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Services;

namespace ImmersiveDisplay.Helpers;

public static partial class UiDispatcher
{
    private static IntPtr _hwnd = IntPtr.Zero;
    private static readonly ConcurrentQueue<Action> _queue = new();
    private static ILoggingService? _loggingService;
    public const int WM_DISPATCH = 0x0400 + 777; // WM_USER + 777

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static void Initialize(IntPtr hwnd, ILoggingService? loggingService = null)
    {
        _hwnd = hwnd;
        if (loggingService != null) _loggingService = loggingService;
        
        if (!_queue.IsEmpty && _hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, WM_DISPATCH, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void BeginInvoke(Action action)
    {
        _queue.Enqueue(action);
        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, WM_DISPATCH, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void InvokePending()
    {
        while (_queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _loggingService?.AddLog($"[UiDispatcher] Error executing action: {ex.Message}");
            }
        }
    }
}
