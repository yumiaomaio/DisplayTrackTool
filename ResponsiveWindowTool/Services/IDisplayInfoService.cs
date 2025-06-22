// File: Services/IDisplayInfoService.cs
using System;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services
{
    // A simple model to return multiple values from a method.
    // In C# 7+, you can also use ValueTuples: (string deviceName, LUID adapterId, uint sourceId).
    public class DisplayIdentifiers
    {
        public string? DeviceName { get; set; }
        public Interop.Structs.LUID AdapterId { get; set; }
        public uint SourceId { get; set; }
    }
    
    // The main service interface
    public interface IDisplayInfoService
    {
        DisplayIdentifiers? GetIdentifiers(IntPtr hwnd);
        DisplaySnapshot? GetCurrentState(IntPtr hwnd);
    }
}