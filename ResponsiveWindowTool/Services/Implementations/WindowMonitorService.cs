// File: Services/Implementations/WindowMonitorService.cs
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ResponsiveWindowTool.Interop;

namespace ResponsiveWindowTool.Services.Implementations
{
    public class WindowMonitorService : IWindowMonitorService, IDisposable
    {
        public event Action<IntPtr, Rect>? WindowStateChanged;
        
        private IntPtr _hookHandle = IntPtr.Zero;
        private readonly NativeMethods.WinEventDelegate _eventDelegate; // Must keep a reference!
        private readonly DispatcherTimer _debounceTimer;
        private IntPtr _lastEventHwnd;

        public WindowMonitorService()
        {
            _eventDelegate = WinEventProc;
            
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(250); // 250ms debounce time
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        public void StartMonitoring(IntPtr hwnd)
        {
            if (_hookHandle != IntPtr.Zero)
            {
                StopMonitoring();
            }
            
            // Hook to a specific process and thread for better performance
            uint processId;
            uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out processId);

            if (processId == 0)
            {
                Debug.WriteLine("[WindowMonitorService] Failed to get process ID. Cannot start monitoring.");
                return;
            }

            _hookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                _eventDelegate,
                processId,
                threadId,
                NativeMethods.WINEVENT_OUTOFCONTEXT);

            if (_hookHandle != IntPtr.Zero)
            {
                Debug.WriteLine($"[WindowMonitorService] Started monitoring HWND {hwnd} on PID {processId}.");
            }
        }

        public void StopMonitoring()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _debounceTimer.Stop();
                Debug.WriteLine("[WindowMonitorService] Stopped monitoring.");
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == NativeMethods.EVENT_OBJECT_LOCATIONCHANGE && idObject == 0 && hwnd != IntPtr.Zero)
            {
                _lastEventHwnd = hwnd;
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            
            IntPtr hwnd = _lastEventHwnd;
            if (hwnd == IntPtr.Zero) return;
            
            if(NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                var windowsRect = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                Debug.WriteLine($"[WindowMonitorService] Debounced event for HWND {hwnd}. New Rect: {windowsRect}");
                WindowStateChanged?.Invoke(hwnd, windowsRect);
            }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}