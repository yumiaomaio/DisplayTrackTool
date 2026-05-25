using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices.Marshalling;

namespace ImmersiveDisplay.Helpers;

[InlineArray(260)]
internal struct Char260Buffer
{
    private char _element0;
}

[InlineArray(14)]
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
                Win32FindDataw pfd = default;
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

    /// <summary>
    /// Resolves a .url (Internet Shortcut) file by manually parsing the INI content.
    /// </summary>
    public static string ResolveUrl(string urlPath)
    {
        if (string.IsNullOrWhiteSpace(urlPath) || !File.Exists(urlPath))
            return urlPath;

        LogAction?.Invoke($"> UrlResolver: Analyzing {Path.GetFileName(urlPath)}");

        try
        {
            // .url files are essentially INI files. We look for URL= under [InternetShortcut]
            foreach (var line in File.ReadLines(urlPath))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    string target = line.Substring(4).Trim();
                    LogAction?.Invoke($"> Url Target: {target}");
                    return target;
                }
            }
        }
        catch (Exception ex)
        {
            LogAction?.Invoke($"> Url Error: {ex.Message}");
        }

        return urlPath;
    }

    public static bool CreateLnk(string shortcutPath, string commandLine)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return false;

            // Protocol URL -- create .url instead of .lnk
            if (commandLine.Contains("://"))
            {
                string urlContent = $"[InternetShortcut]\r\nURL={commandLine}\r\n";
                string urlPath = Path.ChangeExtension(shortcutPath, ".url");
                File.WriteAllText(urlPath, urlContent);
                return true;
            }

            // Parse target and args
            string target;
            string args = "";
            string trimmed = commandLine.Trim();

            if (trimmed.StartsWith("\""))
            {
                int nextQuote = trimmed.IndexOf("\"", 1);
                if (nextQuote != -1)
                {
                    target = trimmed.Substring(1, nextQuote - 1);
                    args = trimmed.Substring(nextQuote + 1).Trim();
                }
                else
                {
                    target = trimmed.Trim('\"');
                }
            }
            else if (!File.Exists(trimmed) && trimmed.Contains(' '))
            {
                int firstSpace = trimmed.IndexOf(' ');
                target = trimmed.Substring(0, firstSpace);
                args = trimmed.Substring(firstSpace + 1).Trim();
            }
            else
            {
                target = trimmed;
            }

            // Create via IShellLink COM
            Guid clsid = new Guid("00021401-0000-0000-C000-000000000046");
            Guid iid = new Guid("000214F9-0000-0000-C000-000000000046");
            int hr = CoCreateInstance(clsid, IntPtr.Zero, 1, iid, out IntPtr pUnknown);
            if (hr < 0) return false;

            try
            {
                var cw = new StrategyBasedComWrappers();
                var obj = cw.GetOrCreateObjectForComInstance(pUnknown, CreateObjectFlags.None);
                var shellLink = (IShellLinkW)obj;
                var persistFile = (IPersistFile)obj;

                shellLink.SetPath(target);
                if (!string.IsNullOrWhiteSpace(args))
                    shellLink.SetArguments(args);

                string finalPath = shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                    ? shortcutPath : Path.ChangeExtension(shortcutPath, ".lnk");
                persistFile.Save(finalPath, true);
                return true;
            }
            finally
            {
                Marshal.Release(pUnknown);
            }
        }
        catch
        {
            return false;
        }
    }

    [GeneratedComInterface]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IShellLinkW
    {
        void GetPath(IntPtr pszFile, int cchMaxPath, out Win32FindDataw pfd, int fFlags);
        void GetIdList(out IntPtr ppidl);
        void SetIdList(IntPtr pidl);
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
        void GetClassId(out Guid pClassID);
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
    internal struct Win32FindDataw
    {
        public uint dwFileAttributes;
        public FILETIME ftCreationTime;
        public FILETIME ftLastAccessTime;
        public FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        public Char260Buffer cFileName;
        public Char14Buffer cAlternateFileName;
    }
}
