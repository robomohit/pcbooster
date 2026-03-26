using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RogCustom.Core;
using RogCustom.Hardware;
using Serilog;

namespace RogCustom.App;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configDir = ConfigPathHelper.GetConfigDirectory();
        var logDir = Path.Combine(configDir, "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });
        services.AddSingleton<IProfileStore, ProfileStore>();
        services.AddSingleton<IAppCapabilitiesService, AppCapabilitiesService>();
        services.AddSingleton<IPowerPlanService, PowerPlanService>();
        services.AddSingleton<ICpuBoostService, CpuBoostService>();
        services.AddSingleton<IModeOrchestrator, ModeOrchestrator>();
        services.AddSingleton<IGameDetectionService, GameDetectionService>();
        services.AddSingleton<IGpuStressTestService, GpuStressTestService>();
        services.AddSingleton<ICpuStressTestService, CpuStressTestService>();
        services.AddSingleton<IProfileManagerService, ProfileManagerService>();
        services.AddSingleton<IGpuControlService>(sp =>
        {
            var gpuLogger = sp.GetRequiredService<ILogger<NvidiaGpuControlService>>();
            var nvidia = new NvidiaGpuControlService(gpuLogger);
            if (nvidia.IsSupported) return nvidia;
            return new StubGpuControlService();
        });
        services.AddSingleton<IFanBridgeService, FanControlBridgeService>();
        services.AddSingleton<SensorBindingLayer>();
        services.AddSingleton<IHardwareMonitor, HardwareMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<HardwareMonitor>>();
            var bindingLayer = sp.GetRequiredService<SensorBindingLayer>();
            var capabilities = sp.GetRequiredService<IAppCapabilitiesService>();
            return new HardwareMonitor(logger, bindingLayer, capabilities);
        });
        services.AddSingleton<ViewModels.DashboardViewModel>();
        services.AddTransient<Views.DashboardView>(sp =>
        {
            var vm = sp.GetRequiredService<ViewModels.DashboardViewModel>();
            return new Views.DashboardView(vm);
        });
        services.AddTransient<ViewModels.ProfilesViewModel>();
        services.AddTransient<Views.ProfilesView>(sp =>
        {
            var vm = sp.GetRequiredService<ViewModels.ProfilesViewModel>();
            return new Views.ProfilesView(vm);
        });
        services.AddTransient<ViewModels.CapabilitiesViewModel>();
        services.AddTransient<Views.CapabilitiesView>(sp =>
        {
            var vm = sp.GetRequiredService<ViewModels.CapabilitiesViewModel>();
            return new Views.CapabilitiesView(vm);
        });
        services.AddTransient<ViewModels.DiagnosticsViewModel>();
        services.AddTransient<Views.DiagnosticsView>(sp =>
        {
            var vm = sp.GetRequiredService<ViewModels.DiagnosticsViewModel>();
            return new Views.DiagnosticsView(vm);
        });
        services.AddTransient<ViewModels.SettingsViewModel>();
        services.AddTransient<Views.SettingsView>(sp =>
        {
            var vm = sp.GetRequiredService<ViewModels.SettingsViewModel>();
            return new Views.SettingsView(vm);
        });
        services.AddTransient<ViewModels.AboutViewModel>();
        services.AddTransient<Views.AboutView>(sp =>
        {
            var vm = sp.GetRequiredService<ViewModels.AboutViewModel>();
            return new Views.AboutView(vm);
        });

        // GpuView has been replaced by HTML/JS bridge

        services.AddTransient<ViewModels.FansViewModel>();
        services.AddTransient<Views.FansView>(sp =>
        {
            var vm = sp.GetRequiredService<ViewModels.FansViewModel>();
            return new Views.FansView(vm);
        });

        ServiceProvider = services.BuildServiceProvider();

        // Initialize capabilities
        var caps = ServiceProvider.GetRequiredService<IAppCapabilitiesService>();
        var gpu = ServiceProvider.GetRequiredService<IGpuControlService>();
        var fan = ServiceProvider.GetRequiredService<IFanBridgeService>();
        
        caps.SetNvidiaGpuControlAvailable(gpu.IsSupported);
        caps.SetFanControlBridgeConnected(fan.IsSupported);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (ServiceProvider?.GetService(typeof(Hardware.IHardwareMonitor)) is IDisposable monitor)
                monitor.Dispose();
        }
        catch { }
        try
        {
            if (ServiceProvider is IDisposable disp)
                disp.Dispose();
        }
        catch { }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
