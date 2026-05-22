using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ImmersiveDisplay;

/// <summary>
/// Manages the bidirectional bridge between the C# engine and the C++ host process.
/// Owns the AppHost lifecycle and the C++ callback dispatch.
/// </summary>
public sealed class HostBridge : IDisposable
{
    private static HostBridge? _current;

    private readonly AppHost _engine;
    private readonly ConcurrentQueue<string> _pendingMessages = new();
    private IntPtr _hostContext = IntPtr.Zero;

    public HostBridge()
    {
        _current = this;

        _engine = new AppHost
        {
            OnStatePush = PushToFrontend,
            IsProtocolAutoStart = false
        };
    }

    /// <summary>
    /// Start the engine and the C++ host window. Blocks until the host window closes.
    /// </summary>
    public unsafe void Run()
    {
        _engine.Initialize();

        NativeHost.Host_Start(&OnJsMessage, &OnWindowResized, &OnHostReady);

        // Host_Start returned — host window closed
        _engine.Dispose();
    }

    public void Dispose()
    {
        _current = null;
        _engine.Dispose();
    }

    // --- C++ → C# callbacks (invoked from host.dll on its thread) ---

    [UnmanagedCallersOnly]
    private static void OnJsMessage(IntPtr jsonPtr)
    {
        try
        {
            var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "";
            var response = _current?._engine.Bridge?.HandleMessage(json) ?? "";
            if (!string.IsNullOrEmpty(response) && _current?._hostContext != IntPtr.Zero)
                _current!.SendToHost(response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostBridge] OnJsMessage error: {ex.Message}");
        }
    }

    [UnmanagedCallersOnly]
    private static void OnWindowResized(int w, int h)
    {
        // Overlay repositioning is handled by WindowMonitorService via WinEvent hooks
    }

    [UnmanagedCallersOnly]
    private static void OnHostReady(IntPtr ctx)
    {
        var bridge = _current;
        if (bridge == null) return;

        bridge._hostContext = ctx;

        // Flush messages queued during initialization (before host was ready)
        while (bridge._pendingMessages.TryDequeue(out var json))
            bridge.SendToHost(json);
    }

    // --- C# → C++ state push ---

    private void PushToFrontend(string json)
    {
        if (_hostContext == IntPtr.Zero)
        {
            _pendingMessages.Enqueue(json);
            return;
        }

        SendToHost(json);
    }

    private void SendToHost(string json)
    {
        var ptr = Marshal.StringToCoTaskMemUTF8(json);
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
