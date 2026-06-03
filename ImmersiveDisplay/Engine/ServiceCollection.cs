using ImmersiveDisplay.Bridge;
using ImmersiveDisplay.Services;
using ImmersiveDisplay.Services.Components;
using ImmersiveDisplay.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace ImmersiveDisplay.Engine;

public static class ServiceCollection
{
    public static void AddImmersiveServices(this IServiceCollection services)
    {
        services.AddCoreServices();
        services.AddHooksAndMonitors();
        services.AddBridge();
    }

    private static void AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ITargetStateManager, TargetStateManager>();
        services.AddSingleton<WindowLayoutManager>();
        services.AddSingleton<DisplayService>();
        services.AddSingleton<OverlayService>();
        services.AddSingleton<TaskbarService>();
        services.AddSingleton<LaunchService>();
        services.AddSingleton<AppIntegrationService>();
    }

    private static void AddHooksAndMonitors(this IServiceCollection services)
    {
        services.AddSingleton<WindowThread>();
        services.AddSingleton<KeyboardHookService>();
        services.AddSingleton<WindowMonitorService>();
    }

    private static void AddBridge(this IServiceCollection services)
    {
        services.AddSingleton<AppBridge>();
    }
}
