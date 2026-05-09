// File: Services/Implementations/TargetStateManager.cs

using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
        private readonly ILoggingService _loggingService;

        // State
        private IntPtr _targetHwnd = IntPtr.Zero;
        private WindowOrientation _lastOrientation = WindowOrientation.Unknown;
        private bool _isRunning = false;
        private WindowSnapshot? _originalSnapshot;

        public event Action<bool>? IsRunningChanged;

        public TargetStateManager(
            IWindowQueryService queryService,
            IWindowMonitorService monitorService,
            IWindowLayoutManager layoutManager,
            IOverlayService overlayService,
            IConfigService configService,
            IKeyboardHookService keyboardHookService,
            ILoggingService loggingService)
        {
            _queryService = queryService;
            _monitorService = monitorService;
            _layoutManager = layoutManager;
            _overlayService = overlayService;
            _configService = configService;
            _keyboardHookService = keyboardHookService;
            _loggingService = loggingService;
        }

        public async Task StartAsync(string processName)
        {
            if (_isRunning)
            {
                AddLog("Already running. Please stop first.");
                return;
            }

            AddLog($"Attempting to start for process: {processName}...");

            _targetHwnd = await Task.Run(() => _queryService.FindWindowByProcessName(processName) ?? IntPtr.Zero);

            if (_targetHwnd == IntPtr.Zero)
            {
                AddLog($"Error: Could not find a visible window for process '{processName}'.");
                return;
            }

            // 检查并还原最小化窗口
            if (NativeMethods.IsIconic(_targetHwnd))
            {
                AddLog("Target window is minimized. Restoring it to normal state before proceeding...");
                NativeMethods.ShowWindow(_targetHwnd, NativeMethods.SW_RESTORE);
            }

            AddLog($"Target window found: HWND {_targetHwnd}.");

            // 备份原始状态
            _originalSnapshot = _layoutManager.TakeSnapshot(_targetHwnd);
            AddLog("Original window styles and position backed up.");

            _isRunning = true;
            _lastOrientation = WindowOrientation.Unknown;

            // --- 背景遮罩开关 ---
            if (_configService.IsBackgroundOverlayEnabled())
            {
                AddLog("Background overlay is ENABLED. Showing overlay.");
                _overlayService.Show(_targetHwnd);
            }
            else
            {
                AddLog("Background overlay is DISABLED. Skipping.");
            }

            _monitorService.WindowStateChanged += OnWindowStateChanged;
            _monitorService.WindowDestroyed += OnWindowDestroyed;
            _monitorService.StartMonitoring(_targetHwnd);

            _keyboardHookService.KeyPressed += OnKeyPressed;
            _keyboardHookService.Start();

            AddLog("Applying initial portrait layout for the window.");
            _layoutManager.ApplyLayout(_targetHwnd, _configService.GetPortraitProfile());
            _lastOrientation = WindowOrientation.Portrait;

            IsRunningChanged?.Invoke(_isRunning);
            AddLog("Service started. Press F12 to stop.");
        }

        public async Task StopAsync()
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
                if (_originalSnapshot != null)
                {
                    AddLog("Restoring target window to original styles and position.");
                    await Task.Run(() => _layoutManager.Restore(_targetHwnd, _originalSnapshot));
                }
                else
                {
                    AddLog("Warning: No original window snapshot found for restoration.");
                }
            }

            // 仅在启用遮罩时隐藏遮罩
            if (_configService.IsBackgroundOverlayEnabled())
            {
                _overlayService.Hide();
            }

            _targetHwnd = IntPtr.Zero;
            _originalSnapshot = null;
            _isRunning = false;

            IsRunningChanged?.Invoke(_isRunning);

            AddLog("Service stopped.");
        }

        private async void OnKeyPressed(int vkCode)
        {
            const int VK_F12 = 0x7B; // F12的虚拟键码

            if (vkCode == VK_F12)
            {
                AddLog("F12 key pressed. Shutting down...");
                await StopAsync();
            }
        }

        private async void OnWindowDestroyed(IntPtr hwnd)
        {
            if (hwnd == _targetHwnd)
            {
                AddLog("Target window was closed. Shutting down automatically.");
                await StopAsync();
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
            _loggingService.AddLog(message);
        }

        public void Dispose()
        {
            _ = StopAsync();
        }
    }
}