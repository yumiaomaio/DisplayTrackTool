using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
[InlineArray(32)]
public struct Char32Buffer
{
    private char _element0;

    public override string ToString()
    {
        unsafe
        {
            fixed (char* p = &_element0)
            {
                return new string(p).TrimEnd('\0');
            }
        }
    }

    public static implicit operator string(Char32Buffer buffer) => buffer.ToString();
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct Devmode
{
    public Char32Buffer dmDeviceName;
    public short dmSpecVersion;
    public short dmDriverVersion;
    public short dmSize;
    public short dmDriverExtra;
    public uint dmFields;

    // Union start
    public short dmOrientation;
    public short dmPaperSize;
    public short dmPaperLength;
    public short dmPaperWidth;
    public short dmScale;
    public short dmCopies;
    public short dmDefaultSource;
    public short dmPrintQuality;
    // Union end

    public short dmColor;
    public short dmDuplex;
    public short dmYResolution;
    public short dmTTOption;
    public short dmCollate;
    public Char32Buffer dmFormName;
    public short dmLogPixels;
    public uint dmBitsPerPel;
    public uint dmPelsWidth;
    public uint dmPelsHeight;
    public uint dmDisplayFlags;
    public uint dmDisplayFrequency;
    public uint dmICMMethod;
    public uint dmICMIntent;
    public uint dmMediaType;
    public uint dmDitherType;
    public uint dmReserved1;
    public uint dmReserved2;
    public uint dmPanningWidth;
    public uint dmPanningHeight;
    public uint dmDisplayOrientation;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct Monitorinfo
{
    public int cbSize;
    public Rect rcMonitor;
    public Rect rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MonitorinfoEx
{
    public int cbSize;
    public Rect rcMonitor;
    public Rect rcWork;
    public uint dwFlags;

    public Char32Buffer szDevice;
}
