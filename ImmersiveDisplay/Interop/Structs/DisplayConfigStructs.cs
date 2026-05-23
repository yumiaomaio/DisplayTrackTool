using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop.Enums;

namespace ImmersiveDisplay.Interop.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct Luid
{
    public uint LowPart;
    public int HighPart;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigDeviceInfo_Header
{
    public DisplayConfigDeviceInfoType type;
    public uint size;
    public Luid adapterId;
    public uint id;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DisplayconfigSourceDeviceName
{
    public DisplayconfigDeviceInfo_Header header;
    public Char32Buffer viewGdiDeviceName;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigPathSourceInfo
{
    public Luid adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigRational
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigPathTargetInfo
{
    public Luid adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint outputTechnology; // <-- 这个字段之前漏掉了，导致后面全部错位！
    public DisplayConfigRotation rotation;
    public uint scaling;
    public DisplayconfigRational refreshRate;
    public uint scanlineOrdering;
    public int targetAvailable; // Win32 BOOL is 4 bytes
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigPathInfo
{
    public DisplayconfigPathSourceInfo sourceInfo;
    public DisplayconfigPathTargetInfo targetInfo;
    public uint flags;
}

[StructLayout(LayoutKind.Sequential)]
public struct Displayconfig2DRegion
{
    public uint cx;
    public uint cy;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigVideoSignalInfo
{
    public ulong pixelRate;
    public DisplayconfigRational hSyncFreq;
    public DisplayconfigRational vSyncFreq;
    public Displayconfig2DRegion activeSize;
    public Displayconfig2DRegion totalSize;
    public uint videoStandard;
    public uint scanlineOrdering;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigTargetMode
{
    public DisplayconfigVideoSignalInfo targetVideoSignalInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigSourceMode
{
    public uint width;
    public uint height;
    public uint pixelFormat;
    public POINTL position;
}

[StructLayout(LayoutKind.Explicit)]
public struct DisplayconfigModeInfo_Union
{
    [FieldOffset(0)]
    public DisplayconfigTargetMode targetMode;
    [FieldOffset(0)]
    public DisplayconfigSourceMode sourceMode;
}

[StructLayout(LayoutKind.Sequential)]
public struct DisplayconfigModeInfo
{
    public DisplayConfigModeInfoType infoType;
    public uint id;
    public Luid adapterId;
    public DisplayconfigModeInfo_Union modeInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct POINTL
{
    public int x;
    public int y;
}
