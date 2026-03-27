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
    private const int ThrottleThresholdC = 88;

    public bool IsStressing { get; private set; }
    public int ProgressPercent { get; private set; }
    public string? StatusMessage { get; private set; }
    public float? MaxGpuTempRecorded { get; private set; }
    public int? LastRigScore { get; private set; }

    // Enhanced telemetry
    public float? MinGpuTempRecorded { get; private set; }
    public float? AvgGpuTempRecorded { get; private set; }
    public float? MaxGpuPowerRecorded { get; private set; }
    public float? AvgGpuPowerRecorded { get; private set; }
    public int? MaxGpuClockRecorded { get; private set; }
    public int? AvgGpuClockRecorded { get; private set; }
    public float? MaxGpuFanRpmRecorded { get; private set; }
    public float? MaxVramUsageMb { get; private set; }
    public int ThrottleEventCount { get; private set; }
    public string? StabilityGrade { get; private set; }

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
        MinGpuTempRecorded = null;
        AvgGpuTempRecorded = null;
        MaxGpuPowerRecorded = null;
        AvgGpuPowerRecorded = null;
        MaxGpuClockRecorded = null;
        AvgGpuClockRecorded = null;
        MaxGpuFanRpmRecorded = null;
        MaxVramUsageMb = null;
        ThrottleEventCount = 0;
        LastRigScore = null;
        StabilityGrade = null;
        StatusMessage = "Initializing GPU Burn-In...";
        
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
            // Start CSV logging with enhanced columns
            var logPath = Path.Combine(
                ConfigPathHelper.GetConfigDirectory(),
                $"gpu_stresstest_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            logWriter = new StreamWriter(logPath);
            await logWriter.WriteLineAsync("Time,GpuTemp,GpuClock,GpuPower,GpuUsage,GpuFanRpm,GpuVramMb,CpuTemp,CpuUsage");

            StatusMessage = "Spawning D3D Rendering Pipeline Workload...";
            
            // Launch WinSAT D3D test for GPU load
            string winSatPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "winsat.exe");
            if (File.Exists(winSatPath))
            {
                stressProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = winSatPath,
                    Arguments = $"d3d -time {totalSeconds} -fullscreen",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }

            int baselineClock = _gpu.CurrentGpuClockMHz ?? 1500;
            float totalGpuUsage = 0f;
            float totalCpuUsage = 0f;
            float totalGpuTemp = 0f;
            float totalGpuPower = 0f;
            long totalGpuClock = 0;
            int sampleCount = 0;
            bool killedByThermal = false;

            for (int i = 0; i < totalSeconds; i++)
            {
                token.ThrowIfCancellationRequested();
                
                await Task.Delay(1000, token);
                var snap = _monitor.GetLastSnapshot();
                
                float gpuTemp = snap.GpuCoreTemp ?? 0f;
                int currentClock = _gpu.CurrentGpuClockMHz ?? baselineClock;
                float gpuUsage = snap.GpuUsagePercent ?? 0f;
                float cpuUsage = snap.CpuUsagePercent ?? 0f;
                float gpuPower = snap.GpuPowerWatts ?? 0f;
                float gpuFanRpm = snap.GpuFanRpm ?? 0f;
                float gpuVramMb = snap.GpuVramUsedMb ?? 0f;
                float cpuTemp = snap.CpuPackageTemp ?? 0f;

                sampleCount++;
                totalGpuUsage += gpuUsage;
                totalCpuUsage += cpuUsage;
                totalGpuTemp += gpuTemp;
                totalGpuPower += gpuPower;
                totalGpuClock += currentClock;

                // Track min/max for all metrics
                if (MaxGpuTempRecorded == null || gpuTemp > MaxGpuTempRecorded) MaxGpuTempRecorded = gpuTemp;
                if (MinGpuTempRecorded == null || (gpuTemp > 0 && gpuTemp < MinGpuTempRecorded)) MinGpuTempRecorded = gpuTemp;
                if (MaxGpuPowerRecorded == null || gpuPower > MaxGpuPowerRecorded) MaxGpuPowerRecorded = gpuPower;
                if (MaxGpuClockRecorded == null || currentClock > MaxGpuClockRecorded) MaxGpuClockRecorded = currentClock;
                if (MaxGpuFanRpmRecorded == null || gpuFanRpm > MaxGpuFanRpmRecorded) MaxGpuFanRpmRecorded = gpuFanRpm;
                if (MaxVramUsageMb == null || gpuVramMb > MaxVramUsageMb) MaxVramUsageMb = gpuVramMb;

                // Log reading
                if (logWriter != null)
                {
                    await logWriter.WriteLineAsync(
                        $"{i},{gpuTemp:F1},{currentClock},{gpuPower:F1},{gpuUsage:F1},{gpuFanRpm:F0},{gpuVramMb:F0},{cpuTemp:F1},{cpuUsage:F1}");
                }

                // Thermal Kill-Switch
                if (gpuTemp >= maxTempLimit)
                {
                    StatusMessage = $"THERMAL KILL-SWITCH at {gpuTemp:F1}C! Test aborted for safety.";
                    _logger.LogCritical("Thermal kill-switch triggered at {Temp}C.", gpuTemp);
                    try { if (stressProcess != null && !stressProcess.HasExited) stressProcess.Kill(); } catch { }
                    _gpu.ResetGpuClocks();
                    killedByThermal = true;
                    break;
                }

                // Throttle detection
                if (gpuTemp >= ThrottleThresholdC || currentClock < (baselineClock * 0.85))
                {
                    ThrottleEventCount++;
                    StatusMessage = $"THROTTLE DETECTED @ {gpuTemp:F1}C | Clock: {currentClock}MHz | Power: {gpuPower:F1}W";
                    _logger.LogWarning("Thermal throttling. Temp: {Temp}C, Clock: {Clock}MHz, Power: {Power}W",
                        gpuTemp, currentClock, gpuPower);
                }
                else
                {
                    StatusMessage = $"Burn-In Active... {currentClock}MHz | {gpuTemp:F1}C | {gpuPower:F1}W | Fan: {gpuFanRpm:F0}RPM";
                }

                ProgressPercent = (int)((i / (float)totalSeconds) * 100);
            }

            if (!killedByThermal)
                token.ThrowIfCancellationRequested();
            
            ProgressPercent = 100;
            
            // Calculate averages
            if (sampleCount > 0)
            {
                AvgGpuTempRecorded = totalGpuTemp / sampleCount;
                AvgGpuPowerRecorded = totalGpuPower / sampleCount;
                AvgGpuClockRecorded = (int)(totalGpuClock / sampleCount);
            }

            // Calculate RIG Score with enhanced formula
            float avgGpuUsage = sampleCount > 0 ? totalGpuUsage / sampleCount : 0f;
            float avgCpuUsage = sampleCount > 0 ? totalCpuUsage / sampleCount : 0f;
            float rawScore = (avgGpuUsage * 4.2f) 
                           + ((MaxGpuClockRecorded ?? 0) * 0.8f) 
                           + (avgCpuUsage * 2.1f)
                           + ((AvgGpuPowerRecorded ?? 0f) * 1.5f)
                           - ((MaxGpuTempRecorded ?? 0f) * 1.5f) 
                           - (ThrottleEventCount * 200f);
            
            LastRigScore = rawScore > 0 ? (int)rawScore : 0;

            // Calculate stability grade
            StabilityGrade = CalculateStabilityGrade(
                ThrottleEventCount, totalSeconds,
                MaxGpuTempRecorded ?? 0f, AvgGpuTempRecorded ?? 0f,
                killedByThermal);

            if (killedByThermal)
                StatusMessage = $"Aborted (thermal). RIG Score: {LastRigScore} | Grade: {StabilityGrade}";
            else if (ThrottleEventCount > 0)
                StatusMessage = $"Done with {ThrottleEventCount} throttle events. RIG Score: {LastRigScore} | Grade: {StabilityGrade}";
            else
                StatusMessage = $"Test Passed! RIG Score: {LastRigScore} | Grade: {StabilityGrade}";
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

    private static string CalculateStabilityGrade(
        int throttleEvents, int totalSeconds,
        float maxTemp, float avgTemp, bool thermalKill)
    {
        if (thermalKill) return "F";

        float throttleRatio = totalSeconds > 0 ? throttleEvents / (float)totalSeconds : 1f;

        if (throttleRatio == 0 && maxTemp < 80f) return "A+";
        if (throttleRatio == 0 && maxTemp < 85f) return "A";
        if (throttleRatio < 0.05f && maxTemp < 88f) return "B+";
        if (throttleRatio < 0.10f) return "B";
        if (throttleRatio < 0.20f) return "C";
        if (throttleRatio < 0.40f) return "D";
        return "F";
    }

    public void Dispose()
    {
        CancelStressTest();
    }
}
