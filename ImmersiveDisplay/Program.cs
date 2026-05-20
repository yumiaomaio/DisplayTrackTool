// File: Program.cs

using System.Runtime.CompilerServices;
using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Interop;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Implementations;
using ImmersiveDisplay.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

[assembly: DisableRuntimeMarshalling]

namespace ImmersiveDisplay;

public static class Program
{
    public static bool IsProtocolAutoStart { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Configure Per-Monitor V2 DPI Awareness before creating any HWNDs
        DpiHelper.ConfigureDpiAwareness();

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
            NativeMethods.TranslateMessage(in msg);
            NativeMethods.DispatchMessage(in msg);
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
