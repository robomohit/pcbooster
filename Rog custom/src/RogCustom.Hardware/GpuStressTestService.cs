using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RogCustom.Core;

namespace RogCustom.Hardware;

public sealed class GpuStressTestService : IGpuStressTestService, IDisposable
{
    private readonly ILogger<GpuStressTestService> _logger;
    private readonly IHardwareMonitor _monitor;
    private readonly IGpuControlService _gpu;
    private CancellationTokenSource? _cts;
    private const int ThrottleThresholdC = 88; // Throttle limit

    public bool IsStressing { get; private set; }
    public int ProgressPercent { get; private set; }
    public string? StatusMessage { get; private set; }
    public float? MaxGpuTempRecorded { get; private set; }
    public int? LastRigScore { get; private set; }

    public GpuStressTestService(ILogger<GpuStressTestService> logger, IHardwareMonitor monitor, IGpuControlService gpu)
    {
        _logger = logger;
        _monitor = monitor;
        _gpu = gpu;
    }

    public void StartStressTest(int durationSeconds, int maxTempLimitC)
    {
        if (IsStressing) return;
        
        IsStressing = true;
        ProgressPercent = 0;
        MaxGpuTempRecorded = null;
        LastRigScore = null;
        StatusMessage = "Initializing OCCT-Style GPU Burn-In...";
        
        _cts = new CancellationTokenSource();
        Task.Run(() => RunStressTestAsync(durationSeconds, maxTempLimitC, _cts.Token));
    }

    public void CancelStressTest()
    {
        if (!IsStressing) return;
        _cts?.Cancel();
    }

    private async Task RunStressTestAsync(int totalSeconds, int maxTempLimit, CancellationToken token)
    {
        Process? stressProcess = null;
        StreamWriter? logWriter = null;

        try
        {
            // Optional: Start logging to CSV
            var logPath = Path.Combine(ConfigPathHelper.GetConfigDirectory(), $"stresstest_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            logWriter = new StreamWriter(logPath);
            await logWriter.WriteLineAsync("Time,GpuTemp,GpuClock,CpuTemp,CpuUsage");

            StatusMessage = "Spawning D3D Rendering Pipeline Workload...";
            
            // Try to launch FurMark if it exists, else fallback to Windows built-in WinSAT D3D test
            string winSatPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "winsat.exe");
            if (File.Exists(winSatPath))
            {
                stressProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = winSatPath,
                    Arguments = $"d3d -time {totalSeconds} -fullscreen", // Try to force heavy D3D load
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }

            int baselineClock = _gpu.CurrentGpuClockMHz ?? 1500;
            int throttleEvents = 0;
            float totalGpuUsage = 0f;
            float totalCpuUsage = 0f;
            int maxObservedClock = 0;

            for (int i = 0; i < totalSeconds; i++)
            {
                token.ThrowIfCancellationRequested();
                
                await Task.Delay(1000, token);
                var snap = _monitor.GetLastSnapshot();
                
                float gpuTemp = snap.GpuCoreTemp ?? 0f;
                int currentClock = _gpu.CurrentGpuClockMHz ?? baselineClock;
                float gpuUsage = snap.GpuUsagePercent ?? 0f;
                float cpuUsage = snap.CpuUsagePercent ?? 0f;

                totalGpuUsage += gpuUsage;
                totalCpuUsage += cpuUsage;
                if (currentClock > maxObservedClock) maxObservedClock = currentClock;

                if (MaxGpuTempRecorded == null || gpuTemp > MaxGpuTempRecorded.Value)
                    MaxGpuTempRecorded = gpuTemp;

                // Log reading
                if (logWriter != null)
                {
                    await logWriter.WriteLineAsync($"{i},{gpuTemp},{currentClock},{snap.CpuPackageTemp ?? 0f},{cpuUsage}");
                }

                // Thermal Kill-Switch
                if (gpuTemp >= maxTempLimit)
                {
                    StatusMessage = $"🚨 TEST ABORTED! THERMAL KILL-SWITCH TRIGGERED AT {gpuTemp:F1}°C";
                    _logger.LogCritical("Thermal kill-switch triggered at {Temp}°C.", gpuTemp);
                    try { if (stressProcess != null && !stressProcess.HasExited) stressProcess.Kill(); } catch { }
                    _gpu.ResetGpuClocks();
                    break; // Abort test loop immediately
                }

                // Throttle detection
                if (gpuTemp >= ThrottleThresholdC || currentClock < (baselineClock * 0.85)) // 15% drop
                {
                    throttleEvents++;
                    StatusMessage = $"⚠️ THERMAL THROTTLE DETECTED! GPU Temp: {gpuTemp:F1}°C";
                    _logger.LogWarning("Stress test detected thermal throttling. Temp: {Temp}°C, Clock: {Clock}MHz", gpuTemp, currentClock);
                }
                else
                {
                    StatusMessage = $"Burn-In Active... Core: {currentClock}MHz | Temp: {gpuTemp:F1}°C";
                }

                ProgressPercent = (int)((i / (float)totalSeconds) * 100);
            }

            token.ThrowIfCancellationRequested();
            
            ProgressPercent = 100;
            
            float avgGpuUsage = totalSeconds > 0 ? totalGpuUsage / totalSeconds : 0f;
            float avgCpuUsage = totalSeconds > 0 ? totalCpuUsage / totalSeconds : 0f;
            float rawScore = (avgGpuUsage * 4.2f) 
                           + (maxObservedClock * 0.8f) 
                           + (avgCpuUsage * 2.1f) 
                           - ((MaxGpuTempRecorded ?? 0f) * 1.5f) 
                           - (throttleEvents * 200f);
            
            LastRigScore = rawScore > 0 ? (int)rawScore : 0;

            if (throttleEvents > 0)
                StatusMessage = $"Test completed with {throttleEvents} throttle events. RIG Score: {LastRigScore}";
            else
                StatusMessage = $"Test Passed successfully! RIG Score: {LastRigScore}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Stress test cancelled.";
            ProgressPercent = 0;
            _logger.LogInformation("Stress test cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Stress test failed with an error.";
            ProgressPercent = 0;
            _logger.LogError(ex, "Stress test failed.");
        }
        finally
        {
            IsStressing = false;
            
            try 
            {
                if (stressProcess != null && !stressProcess.HasExited)
                    stressProcess.Kill();
            } 
            catch { }
            
            if (logWriter != null)
            {
                await logWriter.FlushAsync();
                logWriter.Dispose();
            }
        }
    }

    public void Dispose()
    {
        CancelStressTest();
    }
}
