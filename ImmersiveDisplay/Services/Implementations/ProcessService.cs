// File: Services/Implementations/ProcessService.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;

namespace ImmersiveDisplay.Services.Implementations;

public class ProcessService(ILoggingService loggingService, IDialogService dialogService) : IProcessService
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
                using (var icon = System.Drawing.Icon.FromHandle(hIcon))
                using (var bitmap = icon.ToBitmap())
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
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

    public string? GetProcessCommandLine(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return null;

        string searchName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName.Substring(0, processName.Length - 4)
            : processName;

        var process = Process.GetProcessesByName(searchName).FirstOrDefault();
        if (process == null) return null;

        string? commandLine = GetCommandLineViaKernelQuery(process);
        if (!string.IsNullOrEmpty(commandLine))
        {
            loggingService.AddLog($"[ProcessService] Successfully detected command line for '{processName}'.");
            return commandLine;
        }

        string? path = GetProcessExecutablePath(processName);
        if (path != null)
        {
            loggingService.AddLog($"[ProcessService] Command line detection failed, using executable path fallback for '{processName}'.");

            UiDispatcher.BeginInvoke(() =>
            {
                dialogService.ShowWarning(
                    "权限不足，无法获取目标进程的启动命令行参数（Launch Arguments）。\n\n" +
                    "当前已自动降级为仅获取程序执行文件路径。若要抓取完整的启动参数（如 Steam 或 Epic 游戏的特殊启动参数），请以【管理员身份】重新运行本工具。\n\n" +
                    "-----------------------------------------\n\n" +
                    "Insufficient permissions to capture process startup arguments.\n\n" +
                    "Falling back to executable path only. To capture complete launch parameters (e.g. for Steam/Epic games), please restart this tool as Administrator.",
                    "权限提示 / Permission Warning");
            });

            return path.Contains(' ') ? $"\"{path}\"" : path;
        }

        return null;
    }

    private string? GetCommandLineViaKernelQuery(Process process)
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
                    IntPtr strPtr = Marshal.ReadIntPtr(buffer, IntPtr.Size == 8 ? 8 : 4);
                    
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
        catch (Exception ex)
        {
            loggingService.AddLog($"[ProcessService] Kernel command line query failed: {ex.Message}");
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
        return null;
    }
}
