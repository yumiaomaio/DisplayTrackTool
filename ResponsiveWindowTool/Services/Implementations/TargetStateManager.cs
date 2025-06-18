// File: Services/Implementations/TargetStateManager.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
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
            if (hwnd != _targetHwnd) return;

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