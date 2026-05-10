using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ResponsiveWindowTool.ViewModels;

namespace ResponsiveWindowTool.Bridge
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class AppBridge
    {
        private readonly MainViewModel _viewModel;

        public AppBridge(MainViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        // --- Properties to JS ---
        public string TargetProcessName => _viewModel.TargetProcessName ?? "";
        public string PortraitAspectRatio => _viewModel.PortraitAspectRatio ?? "";
        public bool IsRunning => _viewModel.IsRunning;
        public bool IsAdmin => _viewModel.IsAdmin;
        public bool EnableTaskbarAutoHide => _viewModel.EnableTaskbarAutoHide;
        public bool EnableBackgroundOverlay => _viewModel.EnableBackgroundOverlay;
        public string CurrentImageFileName => _viewModel.CurrentImageFileName ?? "";
        public string BackgroundColor => _viewModel.BackgroundColor;

        // --- Methods from JS ---
        public void StartMonitoring(string processName)
        {
            _viewModel.TargetProcessName = processName;
            _viewModel.StartCommand.Execute(null);
        }

        public void StopMonitoring()
        {
            _viewModel.StopCommand.Execute(null);
        }

        public void SetPortraitAspectRatio(string ratio)
        {
            _viewModel.PortraitAspectRatio = ratio;
        }

        public void SetBackgroundColor(string color)
        {
            _viewModel.BackgroundColor = color;
        }

        public void SetEnableTaskbarAutoHide(bool enable)
        {
            _viewModel.EnableTaskbarAutoHide = enable;
        }

        public void SetEnableBackgroundOverlay(bool enable)
        {
            _viewModel.EnableBackgroundOverlay = enable;
        }

        public void SelectImage()
        {
            _viewModel.SelectImageCommand.Execute(null);
        }

        public void ClearImage()
        {
            _viewModel.ClearImageCommand.Execute(null);
        }

        public string GetImageBase64(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName)) return "";
                string backgroundsDir = Path.Combine(AppContext.BaseDirectory, "Backgrounds");
                string fullPath = Path.Combine(backgroundsDir, fileName);
                if (!File.Exists(fullPath)) return "";

                byte[] imageBytes = File.ReadAllBytes(fullPath);
                string base64String = Convert.ToBase64String(imageBytes);
                string extension = Path.GetExtension(fullPath).ToLower().TrimStart('.');
                // Simple mime type detection
                string mimeType = extension == "png" ? "image/png" : "image/jpeg";
                return $"data:{mimeType};base64,{base64String}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppBridge] Error encoding image to base64: {ex.Message}");
                return "";
            }
        }

        public string GetProcessIconBase64(string processName)
        {
            try
            {
                if (string.IsNullOrEmpty(processName)) return "";
                
                // Remove .exe if present for searching
                string searchName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
                    ? processName.Substring(0, processName.Length - 4) 
                    : processName;

                var processes = Process.GetProcessesByName(searchName);
                var process = processes.FirstOrDefault();
                
                if (process == null) return "";

                string? filePath = null;
                try { filePath = process.MainModule?.FileName; } catch { }

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return "";

                using (Icon icon = Icon.ExtractAssociatedIcon(filePath)!)
                {
                    if (icon == null) return "";
                    using (Bitmap bitmap = icon.ToBitmap())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, ImageFormat.Png);
                        byte[] iconBytes = ms.ToArray();
                        return "data:image/png;base64," + Convert.ToBase64String(iconBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppBridge] Error extracting process icon: {ex.Message}");
                return "";
            }
        }

        public void RestartAsAdmin()
        {
            _viewModel.RestartAsAdminCommand.Execute(null);
        }

        public void ExitApp()
        {
            _viewModel.ExitCommand.Execute(null);
        }

        public void ShowAbout()
        {
            _viewModel.AboutCommand.Execute(null);
        }
        
        // Helper to get logs as a single string (can be optimized later)
        public string[] GetLogs()
        {
            return _viewModel.Logs.ToArray();
        }
    }
}