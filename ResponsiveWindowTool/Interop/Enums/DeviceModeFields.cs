// File: Interop/Enums/DeviceModeFields.cs
using System;

namespace ResponsiveWindowTool.Interop.Enums
{
    [Flags]
    public enum DeviceModeFields : uint
    {
        DM_BITSPERPEL = 0x00040000,
        DM_PELSWIDTH = 0x00080000,
        DM_PELSHEIGHT = 0x00100000,
        DM_DISPLAYFREQUENCY = 0x00400000,
    }
}