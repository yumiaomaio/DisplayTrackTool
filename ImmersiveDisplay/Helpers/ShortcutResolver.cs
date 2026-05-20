using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ImmersiveDisplay.Helpers;

public static class ShortcutResolver
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

            IShellLinkW shellLink;
            try
            {
                shellLink = (IShellLinkW)Marshal.GetObjectForIUnknown(pUnknown);
            }
            finally
            {
                Marshal.Release(pUnknown);
            }

            var persistFile = (IPersistFile)shellLink;
            
            // STGM_READ is 0
            persistFile.Load(lnkPath, 0);

            var sbPath = new StringBuilder(260);
            var sbArgs = new StringBuilder(1024);
            WIN32_FIND_DATAW pfd = default;

            // SLGP_UNCPRIORITY = 2
            shellLink.GetPath(sbPath, sbPath.Capacity, out pfd, 2);
            shellLink.GetArguments(sbArgs, sbArgs.Capacity);

            string target = sbPath.ToString();
            string args = sbArgs.ToString();

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
        catch (Exception ex)
        {
            LogAction?.Invoke($"> Shell Error: {ex.Message}");
            return lnkPath;
        }
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string pszFileName);
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IntPtr ppv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
}
