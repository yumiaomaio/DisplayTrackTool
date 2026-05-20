// File: Interop/NativeMethods.Shell.cs

using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Interop;

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Explicit, Size = 696)]
    public struct Shfileinfo
    {
        [FieldOffset(0)]
        public IntPtr hIcon;
        [FieldOffset(8)]
        public int iIcon;
        [FieldOffset(12)]
        public uint dwAttributes;
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref Shfileinfo psfi, uint cbFileInfo, uint uFlags);

    [LibraryImport("user32.dll", EntryPoint = "PrivateExtractIconsW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PrivateExtractIcons(
        string lpszFile,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        [Out] IntPtr[] phicon,
        [Out] uint[] piconid,
        uint nIcons,
        uint flags);

    public const uint SHGFI_ICON = 0x100;
    public const uint SHGFI_LARGEICON = 0x0;
}
