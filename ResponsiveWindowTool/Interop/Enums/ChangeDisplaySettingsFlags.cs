// File: Interop/Enums/ChangeDisplaySettingsFlags.cs
using System;

namespace ResponsiveWindowTool.Interop.Enums
{
    [Flags]
    public enum ChangeDisplaySettingsFlags : uint
    {
        CDS_NONE = 0,
        CDS_UPDATEREGISTRY = 0x00000001,
        CDS_TEST = 0x00000002,
        CDS_FULLSCREEN = 0x00000004,
        CDS_GLOBAL = 0x00000008,
        CDS_SET_PRIMARY = 0x00000010,
        CDS_NORESET = 0x10000000
    }
}