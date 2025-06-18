// File: Services/Implementations/TargetStateManager.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using ResponsiveWindowTool.Interop;
using ResponsiveWindowTool.Interop.Enums;
using ResponsiveWindowTool.Models;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class TargetStateManager : ITargetStateManager, IDisposable
    {
        // Injected Services
        private readonly IWindowQueryService _queryService;
        private readonly IWindowMonitorService _monitorService;
        private readonly IWindowLayoutManager _layoutManager;
        private readonly IOverlayService _overlayService;

        // State
        private IntPtr _targetHwnd = IntPtr.Zero;
        private WindowOrientation _lastOrientation = WindowOrientation.Unknown;
        private bool _isRunning = false;

        // Pre-defined Layout Profiles
        private readonly LayoutProfile _portraitProfile;
        private readonly LayoutProfile _landscapeProfile;

        // Logging
        public ObservableCollection<string> Logs { get; } = new();

        public TargetStateManager(
            IWindowQueryService queryService,
            IWindowMonitorService monitorService,
            IWindowLayoutManager layoutManager,
            IOverlayService overlayService)
        {
            _queryService = queryService;
            _monitorService = monitorService;
            _layoutManager = layoutManager;
            _overlayService = overlayService;

            // Initialize the layout profiles. These could be loaded from a config file in a real app.
            _portraitProfile = new LayoutProfile
            {
                Name = "Portrait Mode",
                Styles = WindowStyles.WS_POPUP | WindowStyles.WS_VISIBLE,
                ExStyles = WindowExStyles.WS_EX_NONE,
                Sizing = SizingMode.RelativeToScreenHeight,
                Positioning = PositioningMode.CenterScreen,
                AspectRatio = 9.0 / 16.0
            };

            _landscapeProfile = new LayoutProfile
            {
                Name = "Landscape Fullscreen",
                Styles = WindowStyles.WS_POPUP | WindowStyles.WS_VISIBLE,
                ExStyles = WindowExStyles.WS_EX_NONE,
                Sizing = SizingMode.Fullscreen,
                Positioning = PositioningMode.TopLeft,
                AspectRatio = null
            };
        }

        public void Start(string processName)
        {
            if (_isRunning)
            {
                AddLog("Already running. Please stop first.");
                return;
            }

            AddLog($"Attempting to start for process: {processName}...");
            
            _targetHwnd = _queryService.FindWindowByProcessName(processName) ?? IntPtr.Zero;

            if (_targetHwnd == IntPtr.Zero)
            {
                AddLog($"Error: Could not find a visible window for process '{processName}'.");
                return;
            }

            AddLog($"Target window found: HWND {_targetHwnd}.");
            
            _isRunning = true;
            _lastOrientation = WindowOrientation.Unknown; // Reset orientation state

            // Show the background overlay
            _overlayService.Show(_targetHwnd);
            
            // Subscribe to monitor events
            _monitorService.WindowStateChanged += OnWindowStateChanged;
            _monitorService.StartMonitoring(_targetHwnd);

            // Apply initial layout immediately
            AddLog("Applying initial portrait layout.");
            _layoutManager.ApplyLayout(_targetHwnd, _portraitProfile);
            _lastOrientation = WindowOrientation.Portrait;
        }

        public void Stop()
        {
            if (!_isRunning) return;

            AddLog("Stopping service...");
            _monitorService.StopMonitoring();
            _monitorService.WindowStateChanged -= OnWindowStateChanged;
            
            _overlayService.Hide();
            
            // Optional: Restore original window style. For now, we just stop managing it.
            
            _targetHwnd = IntPtr.Zero;
            _isRunning = false;
            AddLog("Service stopped.");
        }

        private void OnWindowStateChanged(IntPtr hwnd, Rect newRect)
        {
            if (hwnd != _targetHwnd || !_isRunning) return;

            // --- 1. 方向改变检测 ---
            var currentOrientation = newRect.Width > newRect.Height 
                ? WindowOrientation.Landscape 
                : WindowOrientation.Portrait;

            if (currentOrientation != _lastOrientation)
            {
                AddLog($"Orientation changed: {_lastOrientation} -> {currentOrientation}");
                _lastOrientation = currentOrientation;

                switch (currentOrientation)
                {
                    case WindowOrientation.Portrait:
                        AddLog("Applying Portrait layout...");
                        _layoutManager.ApplyLayout(_targetHwnd, _portraitProfile);
                        break;
                    case WindowOrientation.Landscape:
                        AddLog("Applying Landscape layout...");
                        _layoutManager.ApplyLayout(_targetHwnd, _landscapeProfile);
                        break;
                }
                // 当方向改变时，布局已完全重置，无需再进行下面的Topmost检查。
                return; 
            }

            // --- 2. Topmost 状态维持 (如果方向没有改变) ---
            var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (!currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                AddLog($"Topmost style lost on HWND {hwnd} in {_lastOrientation} mode. Restoring...");

                if (_lastOrientation == WindowOrientation.Portrait)
                {
                    // 在竖屏模式下，可能整个布局都被重置了，重新应用完整配置更安全
                    AddLog("Re-applying full Portrait layout to ensure consistency.");
                    _layoutManager.ApplyLayout(_targetHwnd, _portraitProfile);
                }
                else if (_lastOrientation == WindowOrientation.Landscape)
                {
                    // 在横屏模式下，窗口已经是全屏，只修复Topmost以避免闪烁
                    AddLog("Patching Topmost style for Landscape mode.");
                    _layoutManager.EnsureTopmost(_targetHwnd);
                }
            }
        }
        
        private void AddLog(string message)
        {
            // Ensure logs are added on the UI thread for data binding
            Application.Current.Dispatcher.Invoke(() =>
            {
                string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                Debug.WriteLine(logEntry); // Also write to debug output
                Logs.Insert(0, logEntry); // Add to top of list
                while (Logs.Count > 100)
                {
                    Logs.RemoveAt(Logs.Count - 1);
                }
            });
        }
        
        public void Dispose()
        {
            Stop();
        }
    }
}