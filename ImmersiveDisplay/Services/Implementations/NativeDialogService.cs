// File: Services/Implementations/NativeDialogService.cs

using System;
using System.Runtime.InteropServices;

namespace ImmersiveDisplay.Services.Implementations;

public class NativeDialogService : IDialogService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter; // Use IntPtr instead of string to preserve embedded null characters
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONINFORMATION = 0x00000040;
    private const uint MB_ICONWARNING = 0x00000030;
    private const uint MB_ICONERROR = 0x00000010;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;

    public void ShowInfo(string message, string title = "Info")
    {
        MessageBox(GetActiveWindow(), message, title, MB_OK | MB_ICONINFORMATION);
    }

    public void ShowWarning(string message, string title = "Warning")
    {
        MessageBox(GetActiveWindow(), message, title, MB_OK | MB_ICONWARNING);
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox(GetActiveWindow(), message, title, MB_OK | MB_ICONERROR);
    }

    public string? ShowOpenFileDialog(string title, string filter)
    {
        // Translate filter format: "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
        // to null-separated string: "Executables (*.exe)\0*.exe\0All Files (*.*)\0*.*\0\0"
        string nullFilter = filter.Replace('|', '\0') + "\0\0";
        
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = GetActiveWindow();
        ofn.lpstrTitle = title;
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;
        
        // Allocate a buffer for the selected file path
        const int maxFileLength = 2048;
        IntPtr fileBuffer = Marshal.AllocHGlobal(maxFileLength * sizeof(char));
        IntPtr filterBuffer = Marshal.StringToHGlobalUni(nullFilter); // Manually marshal to preserve internal nulls
        try
        {
            // Zero-init the file buffer
            byte[] zeros = new byte[maxFileLength * sizeof(char)];
            Marshal.Copy(zeros, 0, fileBuffer, zeros.Length);
            
            ofn.lpstrFile = fileBuffer;
            ofn.nMaxFile = maxFileLength;
            ofn.lpstrFilter = filterBuffer;
            
            if (GetOpenFileName(ref ofn))
            {
                return Marshal.PtrToStringUni(fileBuffer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filterBuffer);
        }

        return null;
    }
}
