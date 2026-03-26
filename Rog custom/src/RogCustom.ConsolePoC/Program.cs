using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RogCustom.Core;
using RogCustom.Hardware;
using Serilog;

namespace RogCustom.ConsolePoC;

static class Program
{
    static void Main(string[] args)
    {
        // Serilog only in composition root; rolling file; optional retention
        var logDir = Path.Combine(ConfigPathHelper.GetConfigDirectory(), "logs");
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logDir, "consolepoc-.log"),
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
        services.AddSingleton<IGpuControlService, StubGpuControlService>();
        services.AddSingleton<IFanBridgeService, StubFanBridgeService>();
        services.AddSingleton<SensorBindingLayer>();
        services.AddSingleton<IHardwareMonitor, HardwareMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<HardwareMonitor>>();
            var bindingLayer = sp.GetRequiredService<SensorBindingLayer>();
            var capabilities = sp.GetRequiredService<IAppCapabilitiesService>();
            return new HardwareMonitor(logger, bindingLayer, capabilities);
        });

        using var provider = services.BuildServiceProvider();

        var monitor = provider.GetRequiredService<IHardwareMonitor>();

        Console.WriteLine("RogCustom ConsolePoC — CPU/GPU temps and fans every 1s. Press Ctrl+C to exit.");
        Console.WriteLine();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var snapshot = monitor.GetLastSnapshot();
                if (monitor.IsLimitedMode)
                    Console.WriteLine("[Limited Mode — Power Plan only; LHM driver did not load]");
                Console.WriteLine("CPU: {0} °C  {1} W  {2} MHz  Fan {3} RPM | GPU: {4} °C  {5} W  {6}%  {7}/{8} MHz  VRAM {9}/{10} MB  Fan {11} RPM | RAM {12}/{13} MB  ({14:O})",
                    snapshot.CpuPackageTemp?.ToString("F1") ?? "—",
                    snapshot.CpuPowerWatts?.ToString("F1") ?? "—",
                    snapshot.CpuEffectiveClockMHz?.ToString("F0") ?? "—",
                    snapshot.CpuFanRpm?.ToString("F0") ?? "—",
                    snapshot.GpuCoreTemp?.ToString("F1") ?? "—",
                    snapshot.GpuPowerWatts?.ToString("F1") ?? "—",
                    snapshot.GpuUsagePercent?.ToString("F0") ?? "—",
                    snapshot.GpuCoreClockMHz?.ToString("F0") ?? "—",
                    snapshot.GpuMemoryClockMHz?.ToString("F0") ?? "—",
                    snapshot.GpuVramUsedMb?.ToString("F0") ?? "—",
                    snapshot.GpuVramTotalMb?.ToString("F0") ?? "—",
                    snapshot.GpuFanRpm?.ToString("F0") ?? "—",
                    snapshot.RamUsedMb?.ToString("F0") ?? "—",
                    snapshot.RamTotalMb?.ToString("F0") ?? "—",
                    snapshot.Timestamp);

                Thread.Sleep(1000);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nExiting...");
        }

        if (monitor is IDisposable d)
            d.Dispose();

        Log.CloseAndFlush();
    }
}
