// File: Interop/Enums/SDCFlags.cs
using System;

namespace ResponsiveWindowTool.Interop.Enums
{
    [Flags]
    public enum SDCFlags : uint
    {
        /// <summary>
        /// The system applies the display settings to the current session.
        /// </summary>
        SDC_APPLY = 0x00000080,

        /// <summary>
        /// The caller requests the best display mode to apply. SetDisplayConfig will find the best-fit mode.
        /// </summary>
        SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020,

        /// <summary>
        /// The caller can force a display path with a target that is not forcibly connected.
        /// </summary>
        SDC_ALLOW_CHANGES = 0x00000400,

        /// <summary>
        /// The settings should be saved to the user's profile.
        /// </summary>
        SDC_SAVE_TO_DATABASE = 0x00000040,

        /// <summary>
        /// The resulting topology is a clone-view.
        /// </summary>
        SDC_TOPOLOGY_CLONE = 0x00000002,

        /// <summary>
        /// The resulting topology is an extend-view.
        /// </summary>
        SDC_TOPOLOGY_EXTEND = 0x00000004,

        /// <summary>
        /// The resulting topology is an internal-view.
        /// </summary>
        SDC_TOPOLOGY_INTERNAL = 0x00000001,

        /// <summary>
        /// The caller provides the topology.
        /// </summary>
        SDC_TOPOLOGY_SUPPLIED = 0x00000010
    }
}