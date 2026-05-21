// File: Helpers/UiDispatcher.cs

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Services;

namespace ImmersiveDisplay.Helpers;

public static partial class UiDispatcher
{
    private static HiddenMessageWindow? _messageWindow;
    private static readonly ConcurrentQueue<Action> Queue = new();
    private static ILoggingService? _loggingService;
    public const int WM_DISPATCH = 0x0400 + 777; // WM_USER + 777

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Initializes the dispatcher by creating a hidden message window on the CURRENT thread.
    /// MUST be called from the main UI STA thread.
    /// </summary>
    public static void Initialize(ILoggingService? loggingService = null)
    {
        if (loggingService != null) _loggingService = loggingService;
        
        if (_messageWindow == null)
        {
            _messageWindow = new HiddenMessageWindow();
        }

        // Install SynchronizationContext for the current (UI) thread
        if (SynchronizationContext.Current == null)
        {
            SynchronizationContext.SetSynchronizationContext(new UiSynchronizationContext());
        }

        if (!Queue.IsEmpty && _messageWindow.Hwnd != IntPtr.Zero)
        {
            PostMessage(_messageWindow.Hwnd, WM_DISPATCH, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void BeginInvoke(Action action)
    {
        Queue.Enqueue(action);
        if (_messageWindow != null && _messageWindow.Hwnd != IntPtr.Zero)
        {
            PostMessage(_messageWindow.Hwnd, WM_DISPATCH, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void Shutdown()
    {
        _messageWindow?.Dispose();
        _messageWindow = null;
    }

    public static void InvokePending()
    {
        while (Queue.TryDequeue(out var action))
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
