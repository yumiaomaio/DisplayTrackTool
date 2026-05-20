// File: Program.cs

using System;
using System.IO;
using System.Threading.Tasks;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Implementations;
using ImmersiveDisplay.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace ImmersiveDisplay;

public static class Program
{
    public static bool IsProtocolAutoStart { get; private set; }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [System.Runtime.InteropServices.DllImport("shcore.dll", SetLastError = true)]
    private static extern int SetProcessDpiAwareness(int value);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDPIAware();

    private static void ConfigureDpiAwareness()
    {
        try
        {
            // Try SetProcessDpiAwarenessContext (Windows 10 1703+)
            // -4 corresponds to DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
            {
                // Fallback 1: SetProcessDpiAwareness (Windows 8.1+)
                // 2 corresponds to PROCESS_PER_MONITOR_DPI_AWARE
                SetProcessDpiAwareness(2);
            }
        }
        catch
        {
            try
            {
                // Fallback 2: SetProcessDPIAware (Windows Vista+)
                SetProcessDPIAware();
            }
            catch
            {
                // Ignore fallback exceptions
            }
        }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        // Configure Per-Monitor V2 DPI Awareness before creating any HWNDs
        ConfigureDpiAwareness();

        // Parse protocol activation arguments
        if (args.Length > 0)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("immersivedisplay://autostart", StringComparison.OrdinalIgnoreCase))
                {
                    IsProtocolAutoStart = true;
                    break;
                }
            }
        }

        // Initialize early asynchronous tasks (warm-up WebView2 environment)
        Task<CoreWebView2Environment> webViewEnvTask = CoreWebView2Environment.CreateAsync();

        // Configure Services
        var services = new ServiceCollection();
        ConfigureServices(services, webViewEnvTask);

        using var serviceProvider = services.BuildServiceProvider();

        // Retrieve MainWindowShell and bootstrap
        var mainWindow = serviceProvider.GetRequiredService<MainWindowShell>();
        mainWindow.Create();
        mainWindow.Show();

        // Win32 Native Message Pump
        NativeMethods.MSG msg;
        while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0) != 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    private static void ConfigureServices(IServiceCollection services, Task<CoreWebView2Environment> webViewEnvTask)
    {
        // Register warm-up WebView2 env task
        services.AddSingleton(webViewEnvTask);

        // Core STATEFUL services
        services.AddSingleton<ITaskbarService, TaskbarService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IPrivilegeService, PrivilegeService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IWindowQueryService, WindowQueryService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<IWindowLayoutManager, WindowLayoutManager>();
        services.AddSingleton<IWindowMonitorService, WindowMonitorService>();
        services.AddSingleton<ITargetStateManager, TargetStateManager>();
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILaunchService, LaunchService>();
        services.AddSingleton<IKeyboardHookService, KeyboardHookService>();
        services.AddSingleton<IProtocolService, ProtocolService>();
        services.AddSingleton<IDialogService, NativeDialogService>();
        services.AddSingleton<IOverlayImageService, OverlayImageService>();
        services.AddSingleton<IAppIntegrationService, AppIntegrationService>();

        // JS Bridges / Web Gateways
        services.AddSingleton<AppBridge>();

        // Native Shell Views
        services.AddSingleton<MainWindowShell>();
    }
}
internal class DummyProgram { } // Placeholder to keep class hierarchy happy if needed
