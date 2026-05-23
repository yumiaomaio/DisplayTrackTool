// File: Helpers/ProcessHelper.cs

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Interop.Structs;

namespace ImmersiveDisplay.Helpers;

public static class ProcessHelper
{
    public static string GetProcessIconBase64(string processName)
    {
        try
        {
            string? filePath = GetProcessExecutablePath(processName);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "";

            IntPtr[] phicon = [IntPtr.Zero];
            uint[] piconid = [0];
            uint extractedCount = NativeMethods.PrivateExtractIcons(filePath, 0, 256, 256, phicon, piconid, 1, 0);
            
            IntPtr hIcon;
            if (extractedCount > 0 && phicon[0] != IntPtr.Zero)
            {
                hIcon = phicon[0];
            }
            else
            {
                Shfileinfo shinfo = default;
                NativeMethods.SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
                hIcon = shinfo.hIcon;
            }

            if (hIcon == IntPtr.Zero) return "";

            try
            {
                using var icon = Icon.FromHandle(hIcon);
                using var bitmap = icon.ToBitmap();
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                byte[] iconBytes = ms.ToArray();
                return "data:image/png;base64," + Convert.ToBase64String(iconBytes);
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }
        catch
        {
            return "";
        }
    }

    public static string? GetProcessExecutablePath(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return null;

        string searchName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var process = Process.GetProcessesByName(searchName).FirstOrDefault();
        if (process == null) return null;

        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    const int bufferSize = 1024;
                    IntPtr buffer = Marshal.AllocHGlobal(bufferSize * sizeof(char));
                    int size = bufferSize;
                    try
                    {
                        if (NativeMethods.QueryFullProcessImageName(hProcess, 0, buffer, ref size))
                        {
                            return Marshal.PtrToStringUni(buffer, size);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    NativeMethods.CloseHandle(hProcess);
                }
            }
        }

        return null;
    }

    public static string? GetProcessCommandLine(string processName, out bool permissionDenied)
    {
        permissionDenied = false;
        if (string.IsNullOrEmpty(processName)) return null;

        string searchName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var process = Process.GetProcessesByName(searchName).FirstOrDefault();
        if (process == null) return null;

        string? commandLine = GetCommandLineViaKernelQuery(process);
        if (!string.IsNullOrEmpty(commandLine))
        {
            return commandLine;
        }

        string? path = GetProcessExecutablePath(processName);
        if (path != null)
        {
            permissionDenied = true;
            return path.Contains(' ') ? $"\"{path}\"" : path;
        }

        return null;
    }

    private static string? GetCommandLineViaKernelQuery(Process process)
    {
        IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_INFORMATION, false, process.Id);
        if (hProcess == IntPtr.Zero) return null;

        try
        {
            int status = NativeMethods.NtQueryInformationProcess(hProcess, NativeMethods.PROCESS_COMMAND_LINE_INFORMATION, IntPtr.Zero, 0, out int length);
            if (length == 0) return null;

            IntPtr buffer = Marshal.AllocHGlobal(length);
            try
            {
                status = NativeMethods.NtQueryInformationProcess(hProcess, NativeMethods.PROCESS_COMMAND_LINE_INFORMATION, buffer, length, out _);
                if (status == 0)
                {
                    short len = Marshal.ReadInt16(buffer);
                    IntPtr strPtr = Marshal.ReadIntPtr(buffer, 8);
                    
                    if (strPtr != IntPtr.Zero && len > 0)
                    {
                        return Marshal.PtrToStringUni(strPtr, len / 2);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // Ignored
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
        return null;
    }
}
