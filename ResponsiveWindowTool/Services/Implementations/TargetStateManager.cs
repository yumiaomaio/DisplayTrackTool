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
        private readonly IDisplayInfoService _displayInfoService;
        private readonly IDisplaySettingService _displaySettingService;

        // State
        private IntPtr _targetHwnd = IntPtr.Zero;
        private WindowOrientation _lastOrientation = WindowOrientation.Unknown;
        private bool _isRunning = false;
        private DisplaySnapshot? _originalDisplaySnapshot;

        // Logging
        public ObservableCollection<string> Logs { get; } = new();

        public event Action<bool>? IsRunningChanged;
        public event Func<string, int, Task<bool>>? ConfirmationRequired;

        public TargetStateManager(
            IWindowQueryService queryService,
            IWindowMonitorService monitorService,
            IWindowLayoutManager layoutManager,
            IOverlayService overlayService,
            IConfigService configService,
            IKeyboardHookService keyboardHookService,
            IDisplayInfoService displayInfoService,
            IDisplaySettingService displaySettingService)
        {
            _queryService = queryService;
            _monitorService = monitorService;
            _layoutManager = layoutManager;
            _overlayService = overlayService;
            _configService = configService;
            _keyboardHookService = keyboardHookService;
            _displayInfoService = displayInfoService;
            _displaySettingService = displaySettingService;
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

            // --- 新增：显示器设置流程 ---

            // 1. 快照当前显示器状态
            AddLog("Taking snapshot of current display settings...");
            _originalDisplaySnapshot = _displayInfoService.GetCurrentState(_targetHwnd);

            if (_originalDisplaySnapshot == null)
            {
                AddLog("Error: Failed to get current display state. Aborting start.");
                _targetHwnd = IntPtr.Zero; // 重置目标，防止后续操作
                return;
            }

            AddLog(
                $"Snapshot: {_originalDisplaySnapshot.Width}x{_originalDisplaySnapshot.Height} @ {_originalDisplaySnapshot.Dpi}% on device '{_originalDisplaySnapshot.DeviceName}'");

            // 2. 从配置获取目标设置
            var targetResolution = _configService.GetTargetResolution();
            AddLog(
                $"Target settings from config: {targetResolution.Width}x{targetResolution.Height} @ {targetResolution.Dpi}%");

            // 3. 应用新设置
            // 获取操作所需的ID
            var identifiers = _displayInfoService.GetIdentifiers(_targetHwnd);
            if (identifiers == null)
            {
                AddLog("Error: Failed to get display identifiers. Aborting start.");
                _targetHwnd = IntPtr.Zero;
                return;
            }

            AddLog("Applying target display settings...");
            bool settingsApplied = _displaySettingService.ApplySettings(
                identifiers.DeviceName!,
                targetResolution.Width,
                targetResolution.Height,
                (uint)targetResolution.Dpi,
                identifiers.AdapterId,
                identifiers.SourceId
            );

            if (!settingsApplied)
            {
                AddLog("Error: Failed to apply target display settings. Aborting and attempting to restore.");
                // 尝试立即恢复
                _displaySettingService.ApplySettings(
                    _originalDisplaySnapshot.DeviceName,
                    _originalDisplaySnapshot.Width,
                    _originalDisplaySnapshot.Height,
                    _originalDisplaySnapshot.Dpi,
                    identifiers.AdapterId,
                    identifiers.SourceId);
                _targetHwnd = IntPtr.Zero;
                return;
            }

            AddLog("Target display settings applied successfully.");

            // --- 现有逻辑继续 ---

            _isRunning = true;
            _lastOrientation = WindowOrientation.Unknown;

            _overlayService.Show(_targetHwnd);

            _monitorService.WindowStateChanged += OnWindowStateChanged;
            _monitorService.WindowDestroyed += OnWindowDestroyed;
            _monitorService.StartMonitoring(_targetHwnd);

            _keyboardHookService.KeyPressed += OnKeyPressed;
            _keyboardHookService.Start();

            // 在显示器设置完成后，稍作延时再应用窗口布局，给系统反应时间
            System.Threading.Thread.Sleep(200);

            AddLog("Applying initial portrait layout for the window.");
            _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
            _lastOrientation = WindowOrientation.Portrait;

            IsRunningChanged?.Invoke(_isRunning);
        }

        public async void Stop()
        {
            if (!_isRunning) return;

            AddLog("Stopping service...");

            _keyboardHookService.Stop();
            _keyboardHookService.KeyPressed -= OnKeyPressed;
            _monitorService.StopMonitoring();
            _monitorService.WindowStateChanged -= OnWindowStateChanged;
            _monitorService.WindowDestroyed -= OnWindowDestroyed;

            if (_targetHwnd != IntPtr.Zero && NativeMethods.IsWindow(_targetHwnd))
            {
                AddLog("Restoring target window to standard style.");
                _layoutManager.RestoreToStandard(_targetHwnd);
            }

            _overlayService.Hide();

            // --- 新增：恢复显示器设置流程 ---
            if (_originalDisplaySnapshot != null)
            {
                bool shouldRestore = true; // 默认恢复
                if (_configService.IsConfirmationRequired() && ConfirmationRequired != null)
                {
                    AddLog("Confirmation required to restore display settings...");
                    // 触发事件，并等待ViewModel的响应
                    shouldRestore = await ConfirmationRequired("Do you want to restore the original display settings?", 10);
                }

                if (shouldRestore)
                {
                    AddLog("Restoring original display settings...");

                    // 获取最新的ID，因为显示器配置可能已变
                    var identifiers = _displayInfoService.GetIdentifiers(_targetHwnd);
                    if (identifiers != null)
                    {
                        bool restored = _displaySettingService.ApplySettings(
                            _originalDisplaySnapshot.DeviceName,
                            _originalDisplaySnapshot.Width,
                            _originalDisplaySnapshot.Height,
                            _originalDisplaySnapshot.Dpi,
                            identifiers.AdapterId,
                            identifiers.SourceId
                        );

                        if (restored)
                        {
                            AddLog("Original display settings restored successfully.");
                        }
                        else
                        {
                            AddLog("Warning: Failed to restore original display settings. Manual adjustment may be required.");
                        }
                    }
                    else
                    {
                        AddLog("Warning: Could not get display identifiers to restore settings.");
                    }
                }
                else
                {
                    AddLog("Restore skipped by user. The new display settings will be kept.");
                }
                _originalDisplaySnapshot = null; // 清理快照
            }

            _targetHwnd = IntPtr.Zero;
            _isRunning = false;

            IsRunningChanged?.Invoke(_isRunning);

            AddLog("Service stopped.");
        }

        private void OnKeyPressed(int vkCode)
        {
            // 原始代码: const int VK_ESCAPE = 0x1B;
            const int VK_F12 = 0x7B; // F12的虚拟键码

            if (vkCode == VK_F12)
            {
                AddLog("F12 key pressed. Shutting down and restoring settings...");

                // 调用Stop()来执行所有清理工作
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