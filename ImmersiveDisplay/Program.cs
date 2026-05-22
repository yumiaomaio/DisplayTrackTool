using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Native;

namespace ImmersiveDisplay;

internal class Program
{
    private static ImmersiveEngine? _engine;
    private static IntPtr _hostContext = IntPtr.Zero;
    private static readonly ConcurrentQueue<string> _pendingMessages = new();

    [STAThread]
    static unsafe void Main()
    {
        // 1. Initialize engine + services + overlay thread
        //    OnStatePush is set BEFORE Initialize() so state pushes during init
        //    are captured (buffered until host.dll is ready via _pendingMessages).
        _engine = new ImmersiveEngine();
        _engine.OnStatePush = PushToFrontend;
        _engine.Initialize();

        // 3. Start C++ host window with WebView2 (blocks until window closes)
        NativeHost.Host_Start(&OnJsMessage, &OnWindowResized, &OnHostReady);

        // 4. Cleanup
        if (_engine.Bridge != null)
        {
            _engine.Bridge.OnMessageSent -= PushToFrontend;
        }
        _engine.Dispose();
    }

    [UnmanagedCallersOnly]
    private static void OnJsMessage(IntPtr jsonPtr)
    {
        try
        {
            string json = Marshal.PtrToStringUTF8(jsonPtr) ?? "";
            string response = _engine?.Bridge?.HandleMessage(json) ?? "";
            if (!string.IsNullOrEmpty(response) && _hostContext != IntPtr.Zero)
            {
                IntPtr ptr = Marshal.StringToCoTaskMemUTF8(response);
                try { NativeHost.Host_PostMessage(_hostContext, ptr); }
                finally { Marshal.FreeCoTaskMem(ptr); }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Program] OnJsMessage error: {ex.Message}");
        }
    }

    [UnmanagedCallersOnly]
    private static void OnWindowResized(int w, int h)
    {
        // Overlay repositioning is handled by OverlayHost/WindowMonitorService via WinEvent hooks
    }

    [UnmanagedCallersOnly]
    private static void OnHostReady(IntPtr ctx)
    {
        _hostContext = ctx;

        // Flush messages queued during initialization (before host was ready)
        while (_pendingMessages.TryDequeue(out var json))
        {
            SendToHost(json);
        }
    }

    private static void PushToFrontend(string json)
    {
        // If host isn't ready yet, queue for later delivery
        if (_hostContext == IntPtr.Zero)
        {
            _pendingMessages.Enqueue(json);
            return;
        }

        SendToHost(json);
    }

    private static void SendToHost(string json)
    {
        IntPtr ptr = Marshal.StringToCoTaskMemUTF8(json);
        try
        {
            NativeHost.Host_PostMessage(_hostContext, ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }
}
