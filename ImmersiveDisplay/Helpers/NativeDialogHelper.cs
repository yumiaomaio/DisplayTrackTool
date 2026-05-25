// File: Helpers/NativeDialogHelper.cs

using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Helpers;

public static partial class NativeDialogHelper
{
    [LibraryImport("user32.dll")]
    private static partial IntPtr GetActiveWindow();

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [StructLayout(LayoutKind.Sequential)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [LibraryImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetOpenFileName(ref OPENFILENAME ofn);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONINFORMATION = 0x00000040;
    private const uint MB_ICONWARNING = 0x00000030;
    private const uint MB_ICONERROR = 0x00000010;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;

    public static void ShowInfo(string message, string title = "Info")
    {
        MessageBox(GetActiveWindow(), message, title, MB_OK | MB_ICONINFORMATION);
    }

    public static void ShowInfo(DialogKey message, DialogKey? title = null, params string[] args)
    {
        ShowInfo(DialogMessages.Format(message, args),
                 title.HasValue ? DialogMessages.Get(title.Value) : "Info");
    }

    public static void ShowWarning(string message, string title = "Warning")
    {
        MessageBox(GetActiveWindow(), message, title, MB_OK | MB_ICONWARNING);
    }

    public static void ShowWarning(DialogKey message, DialogKey? title = null, params string[] args)
    {
        ShowWarning(DialogMessages.Format(message, args),
                    title.HasValue ? DialogMessages.Get(title.Value) : "Warning");
    }

    public static void ShowError(string message, string title = "Error")
    {
        MessageBox(GetActiveWindow(), message, title, MB_OK | MB_ICONERROR);
    }

    public static void ShowError(DialogKey message, params string[] args)
    {
        ShowError(DialogMessages.Format(message, args), "Error");
    }

    public static string? ShowOpenFileDialog(string title, string filter)
    {
        string nullFilter = filter.Replace('|', '\0') + "\0\0";
        
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = GetActiveWindow();
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;
        
        const int maxFileLength = 2048;
        IntPtr fileBuffer = Marshal.AllocHGlobal(maxFileLength * sizeof(char));
        IntPtr filterBuffer = Marshal.StringToHGlobalUni(nullFilter);
        IntPtr titleBuffer = Marshal.StringToHGlobalUni(title);
        try
        {
            byte[] zeros = new byte[maxFileLength * sizeof(char)];
            Marshal.Copy(zeros, 0, fileBuffer, zeros.Length);
            
            ofn.lpstrFile = fileBuffer;
            ofn.nMaxFile = maxFileLength;
            ofn.lpstrFilter = filterBuffer;
            ofn.lpstrTitle = titleBuffer;
            
            if (GetOpenFileName(ref ofn))
            {
                return Marshal.PtrToStringUni(fileBuffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filterBuffer);
            Marshal.FreeHGlobal(titleBuffer);
        }

        return null;
    }

    public static string? ShowOpenFileDialog(DialogKey title, string filter)
    {
        return ShowOpenFileDialog(DialogMessages.Get(title), filter);
    }
}
