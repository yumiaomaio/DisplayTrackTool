using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

namespace ImmersiveDisplay.Helpers;

[System.Runtime.CompilerServices.InlineArray(260)]
internal struct Char260Buffer
{
    private char _element0;
}

[System.Runtime.CompilerServices.InlineArray(14)]
internal struct Char14Buffer
{
    private char _element0;
}

public static partial class ShortcutResolver
{
    public static Action<string>? LogAction { get; set; }

    /// <summary>
    /// Resolves a .lnk shortcut file using native IShellLink COM interface.
    /// </summary>
    public static string Resolve(string lnkPath)
    {
        if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath))
            return lnkPath;

        LogAction?.Invoke($"> NativeShellLink: Analyzing {Path.GetFileName(lnkPath)}");

        try
        {
            Guid clsid = new Guid("00021401-0000-0000-C000-000000000046");
            Guid iid = new Guid("000214F9-0000-0000-C000-000000000046");
            int hr = CoCreateInstance(clsid, IntPtr.Zero, 1, iid, out IntPtr pUnknown);
            if (hr < 0)
            {
                LogAction?.Invoke($"> Shell Error: CoCreateInstance failed with HRESULT 0x{hr:X}");
                return lnkPath;
            }

            try
            {
                var cw = new StrategyBasedComWrappers();
                var obj = cw.GetOrCreateObjectForComInstance(pUnknown, CreateObjectFlags.None);
                var shellLink = (IShellLinkW)obj;
                var persistFile = (IPersistFile)obj;

                // STGM_READ is 0
                persistFile.Load(lnkPath, 0);

                IntPtr pszPath = Marshal.AllocHGlobal(260 * sizeof(char));
                IntPtr pszArgs = Marshal.AllocHGlobal(1024 * sizeof(char));
                WIN32_FIND_DATAW pfd = default;
                try
                {
                    // SLGP_UNCPRIORITY = 2
                    shellLink.GetPath(pszPath, 260, out pfd, 2);
                    shellLink.GetArguments(pszArgs, 1024);

                    string target = Marshal.PtrToStringUni(pszPath) ?? string.Empty;
                    string args = Marshal.PtrToStringUni(pszArgs) ?? string.Empty;

                    LogAction?.Invoke($"> Shell Target: {target}");
                    if (!string.IsNullOrWhiteSpace(args))
                        LogAction?.Invoke($"> Shell Args: {args}");

                    if (string.IsNullOrWhiteSpace(args))
                        return target;

                    // Handle quoting if needed
                    if (target.Contains(' ') && !target.StartsWith("\""))
                        target = $"\"{target}\"";

                    return $"{target} {args}";
                }
                finally
                {
                    Marshal.FreeHGlobal(pszPath);
                    Marshal.FreeHGlobal(pszArgs);
                }
            }
            finally
            {
                Marshal.Release(pUnknown);
            }
        }
        catch (Exception ex)
        {
            LogAction?.Invoke($"> Shell Error: {ex.Message}");
            return lnkPath;
        }
    }

    [GeneratedComInterface]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IShellLinkW
    {
        void GetPath(IntPtr pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(IntPtr pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory(IntPtr pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments(IntPtr pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(IntPtr pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [GeneratedComInterface]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile(out IntPtr ppszFileName);
    }

    [LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
    private static partial int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        public Char260Buffer cFileName;
        public Char14Buffer cAlternateFileName;
    }
}
