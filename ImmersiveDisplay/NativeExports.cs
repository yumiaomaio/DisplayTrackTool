using System.Runtime.InteropServices;
using ImmersiveDisplay.Helpers;

namespace ImmersiveDisplay.Native;

public static class NativeExports
{
    public delegate void MessageCallback(IntPtr jsonPtr);

    private static GCHandle _engineHandle;

    [UnmanagedCallersOnly(EntryPoint = "immersive_create")]
    public static IntPtr Create()
    {
        try
        {
            var engine = new ImmersiveEngine();
            _engineHandle = GCHandle.Alloc(engine);
            return GCHandle.ToIntPtr(_engineHandle);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "immersive_initialize")]
    public unsafe static void Initialize(IntPtr handle, delegate* unmanaged<IntPtr, void> callback)
    {
        if (handle == IntPtr.Zero) return;
        
        try
        {
            var engine = (ImmersiveEngine?)GCHandle.FromIntPtr(handle).Target;
            if (engine == null) return;

            engine.Initialize();

            if (engine.Bridge != null)
            {
                engine.Bridge.OnMessageSent += (json) =>
                {
                    IntPtr ptr = Marshal.StringToCoTaskMemUTF8(json);
                    try
                    {
                        callback(ptr);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(ptr);
                    }
                };
            }
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "immersive_set_protocol_autostart")]
    public static void SetProtocolAutoStart(IntPtr handle, int isAutoStart)
    {
        if (handle == IntPtr.Zero) return;
        var engine = (ImmersiveEngine?)GCHandle.FromIntPtr(handle).Target;
        if (engine != null)
        {
            engine.IsProtocolAutoStart = isAutoStart != 0;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "immersive_handle_message")]
    public static IntPtr HandleMessage(IntPtr handle, IntPtr jsonPtr)
    {
        if (handle == IntPtr.Zero || jsonPtr == IntPtr.Zero) return IntPtr.Zero;

        try
        {
            var engine = (ImmersiveEngine?)GCHandle.FromIntPtr(handle).Target;
            if (engine == null || engine.Bridge == null) return IntPtr.Zero;

            string json = Marshal.PtrToStringUTF8(jsonPtr) ?? "";
            string response = engine.Bridge.HandleMessage(json);
            
            return Marshal.StringToCoTaskMemUTF8(response);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "immersive_free_string")]
    public static void FreeString(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "immersive_dispose")]
    public static void Dispose(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;

        try
        {
            var engine = (ImmersiveEngine?)GCHandle.FromIntPtr(handle).Target;
            if (engine != null)
            {
                engine.Dispose();
            }
            
            if (_engineHandle.IsAllocated)
            {
                _engineHandle.Free();
            }
        }
        catch
        {
        }
    }
}
