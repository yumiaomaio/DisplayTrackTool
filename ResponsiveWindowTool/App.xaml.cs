// File: App.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using ResponsiveWindowTool.Services;
using ResponsiveWindowTool.Services.Implementations;
using ResponsiveWindowTool.ViewModels;
using ResponsiveWindowTool.Views;
using System.Windows;

namespace ResponsiveWindowTool
{
    public partial class App : Application
    {
        private readonly ServiceProvider _serviceProvider;

        public App()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Register services as Singleton since this is a stateful desktop tool
            services.AddSingleton<IWindowQueryService, WindowQueryService>();
            services.AddSingleton<IOverlayService, OverlayService>();
            services.AddSingleton<IWindowLayoutManager, WindowLayoutManager>();
            services.AddSingleton<IWindowMonitorService, WindowMonitorService>();
            services.AddSingleton<ITargetStateManager, TargetStateManager>();

            // Register ViewModels
            services.AddSingleton<MainViewModel>();

            // Register Views
            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}