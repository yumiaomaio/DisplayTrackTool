// File: Models/DisplayInfo.cs
namespace ResponsiveWindowTool.Models
{
    public class DisplayModeInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class DpiScalingInfo
    {
        public uint Current { get; set; } = 100;
        public uint Recommended { get; set; } = 100;
        public bool IsInitialized { get; set; } = false;
    }
}