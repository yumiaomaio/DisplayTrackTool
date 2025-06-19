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
        private readonly IConfigService _configService;
        private readonly IWindowQueryService _queryService;
        private readonly IWindowMonitorService _monitorService;
        private readonly IWindowLayoutManager _layoutManager;
        private readonly IOverlayService _overlayService;
        private readonly IKeyboardHookService _keyboardHookService;

        // State
        private IntPtr _targetHwnd = IntPtr.Zero;
        private WindowOrientation _lastOrientation = WindowOrientation.Unknown;
        private bool _isRunning = false;

        // Logging
        public ObservableCollection<string> Logs { get; } = new();
        
        public event Action<bool>? IsRunningChanged;

        public TargetStateManager(
            IWindowQueryService queryService,
            IWindowMonitorService monitorService,
            IWindowLayoutManager layoutManager,
            IOverlayService overlayService,
            IConfigService configService,
            IKeyboardHookService keyboardHookService)
        {
            _queryService = queryService;
            _monitorService = monitorService;
            _layoutManager = layoutManager;
            _overlayService = overlayService;
            _configService = configService;
            _keyboardHookService = keyboardHookService;

            // 移除 LayoutProfile 缓存
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
            IsRunningChanged?.Invoke(_isRunning);
            _lastOrientation = WindowOrientation.Unknown; // Reset orientation state

            // Show the background overlay
            _overlayService.Show(_targetHwnd);
            
            // Subscribe to monitor events
            _monitorService.WindowStateChanged += OnWindowStateChanged;
            _monitorService.WindowDestroyed += OnWindowDestroyed;
            _monitorService.StartMonitoring(_targetHwnd);

            // Apply initial layout immediately
            AddLog("Applying initial portrait layout.");
            _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
            _lastOrientation = WindowOrientation.Portrait;
            
            _keyboardHookService.KeyPressed += OnKeyPressed;
            _keyboardHookService.Start();
        }

        public void Stop()
        {
            if (!_isRunning) return;

            AddLog("Stopping service...");

            // 停止钩子，防止在恢复样式时触发不必要的事件
            _keyboardHookService.Stop();
            _keyboardHookService.KeyPressed -= OnKeyPressed;
            _monitorService.StopMonitoring();
            _monitorService.WindowStateChanged -= OnWindowStateChanged;
            _monitorService.WindowDestroyed -= OnWindowDestroyed;

            // 在隐藏背景之前，先恢复目标窗口的样式
            if (_targetHwnd != IntPtr.Zero)
            {
                // IsWindow 是一个好的安全检查，确保句柄仍然有效
                if (NativeMethods.IsWindow(_targetHwnd))
                {
                    AddLog("Restoring target window to standard style.");
                    _layoutManager.RestoreToStandard(_targetHwnd);
                }
            }
        
            // 隐藏背景
            _overlayService.Hide();

            // 清理状态
            _targetHwnd = IntPtr.Zero;
            _isRunning = false;
        
            // 触发状态更新事件
            IsRunningChanged?.Invoke(_isRunning); 
        
            AddLog("Service stopped.");
        }
    
        private void OnKeyPressed(int vkCode)
        {
            const int VK_ESCAPE = 0x1B;
            if (vkCode == VK_ESCAPE)
            {
                AddLog("ESC key pressed. Shutting down and restoring window...");
            
                // ESC键现在只需要调用Stop()即可，因为所有恢复逻辑都在Stop()里了
                Stop();
            }
        }
        
        private void OnWindowDestroyed(IntPtr hwnd)
        {
            if (hwnd == _targetHwnd)
            {
                AddLog("Target window was closed. Shutting down automatically.");
                // 调用Stop()来执行所有清理工作
                Stop();
            }
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
                        _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
                        break;
                    case WindowOrientation.Landscape:
                        AddLog("Applying Landscape layout...");
                        _layoutManager.ApplyLayout(_targetHwnd, _configService.GetLandscapeProfile());
                        break;
                }
                return; 
            }

            // --- 2. Topmost 状态维持 (如果方向没有改变) ---
            var currentExStyle = (WindowExStyles)NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            if (!currentExStyle.HasFlag(WindowExStyles.WS_EX_TOPMOST))
            {
                AddLog($"Topmost style lost on HWND {hwnd} in {_lastOrientation} mode. Restoring...");

                if (_lastOrientation == WindowOrientation.Portrait)
                {
                    AddLog("Re-applying full Portrait layout to ensure consistency.");
                    _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
                }
                else if (_lastOrientation == WindowOrientation.Landscape)
                {
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