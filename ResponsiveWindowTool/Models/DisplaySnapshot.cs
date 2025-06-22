// File: Models/DisplaySnapshot.cs
namespace ResponsiveWindowTool.Models
{
    public class DisplaySnapshot
    {
        public string DeviceName { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public uint Dpi { get; set; }
    }
}