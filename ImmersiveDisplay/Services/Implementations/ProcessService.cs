using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ImmersiveDisplay.Interop;

namespace ImmersiveDisplay.Services.Implementations;

public class ProcessService(ILoggingService loggingService) : IProcessService
{
    public string GetProcessIconBase64(string processName)
    {
        try
        {
            string? filePath = GetProcessExecutablePath(processName);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "";

            // 1. 尝试提取 256x256 高清图标
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
                // 2. 兜底:使用 ShellInfo (通常为 32x32)
                var shinfo = new NativeMethods.Shfileinfo();
                NativeMethods.SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
                hIcon = shinfo.hIcon;
            }

            if (hIcon == IntPtr.Zero) return "";

            try
            {
                var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    encoder.Save(ms);
                    byte[] iconBytes = ms.ToArray();
                    return "data:image/png;base64," + Convert.ToBase64String(iconBytes);
                }
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProcessService] Error extracting icon for '{processName}': {ex.Message}");
            return "";
        }
    }

    public string? GetProcessExecutablePath(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return null;

        string searchName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName.Substring(0, processName.Length - 4)
            : processName;

        var process = Process.GetProcessesByName(searchName).FirstOrDefault();
        if (process == null) return null;

        // 尝试标准方法
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            // 权限不足时的 Fallback
            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    var sb = new StringBuilder(1024);
                    int size = sb.Capacity;
                    if (NativeMethods.QueryFullProcessImageName(hProcess, 0, sb, ref size))
                    {
                        return sb.ToString();
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

    public string? GetParentProcessName()
    {
        try
        {
            var pbi = new NativeMethods.PROCESS_BASIC_INFORMATION();
            int returnLength;
            int status = NativeMethods.NtQueryInformationProcess(
                Process.GetCurrentProcess().Handle, 
                0, // ProcessBasicInformation
                ref pbi, 
                Marshal.SizeOf(pbi), 
                out returnLength);

            if (status != 0) return null; // NT_SUCCESS

            int parentPid = pbi.InheritedFromUniqueProcessId.ToInt32();
            if (parentPid <= 0) return null;

            using var parentProcess = Process.GetProcessById(parentPid);
            return parentProcess.ProcessName;
        }
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProcessService] Failed to get parent process: {ex.Message}");
            return null;
        }
    }
}
