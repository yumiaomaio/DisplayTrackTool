using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Helpers;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace ImmersiveDisplay;

public static class ServiceCollectionExtensions
{
    public static void AddImmersiveServices(this IServiceCollection services)
    {
        services.AddCoreServices();
        services.AddHooksAndMonitors();
        services.AddBridge();
    }

    private static void AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IPrivilegeService, PrivilegeService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<ITaskbarService, TaskbarService>();
        services.AddSingleton<IWindowQueryService, WindowQueryService>();
        services.AddSingleton<IWindowLayoutManager, WindowLayoutManager>();
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILaunchService, LaunchService>();
        services.AddSingleton<IProtocolService, ProtocolService>();
        services.AddSingleton<IDialogService, NativeDialogService>();
        services.AddSingleton<IOverlayService, OverlayService>();
        services.AddSingleton<IOverlayImageService, OverlayImageService>();
        services.AddSingleton<ITargetStateManager, TargetStateManager>();
        services.AddSingleton<IAppIntegrationService, AppIntegrationService>();
    }

    private static void AddHooksAndMonitors(this IServiceCollection services)
    {
        services.AddSingleton<WindowThread>();
        services.AddSingleton<IKeyboardHookService, KeyboardHookService>();
        services.AddSingleton<IWindowMonitorService, WindowMonitorService>();
    }

    private static void AddBridge(this IServiceCollection services)
    {
        services.AddSingleton<AppBridge>();
    }
}
