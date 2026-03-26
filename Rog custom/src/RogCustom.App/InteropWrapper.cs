using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using RogCustom.Core;
using RogCustom.Hardware;
using RogCustom.App.ViewModels;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace RogCustom.App;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class InteropWrapper
{
    private readonly DashboardViewModel _dashboardVm;

    public InteropWrapper()
    {
        try
        {
            _dashboardVm = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
        }
        catch (Exception ex)
        {
            // Log the DI failure gracefully instead of producing cryptic JS errors
            System.Diagnostics.Debug.WriteLine($"InteropWrapper DI init failed: {ex.Message}");
            throw new InvalidOperationException(
                "Failed to initialize hardware bridge. Some features may be unavailable.", ex);
        }
    }

    public string GetStats()
    {
        // Sensor rebinding is expensive -- only do it on explicit user request,
        // not on every poll cycle. The background HardwareMonitor thread handles
        // periodic updates automatically.
        
        var stats = new
        {
            cpuTemp = _dashboardVm.CpuTempValue,
            cpuUsage = _dashboardVm.CpuUsageValue,
            cpuPower = _dashboardVm.CpuPowerValue,
            gpuTemp = _dashboardVm.GpuTempValue,
            gpuUsage = _dashboardVm.GpuUsageValue,
            gpuPower = _dashboardVm.GpuPowerValue,
            ramUsage = _dashboardVm.RamUsagePercent,
            vramUsage = _dashboardVm.GpuVramPercent,
            cpuFan = _dashboardVm.CpuFanRpm,
            gpuFan = _dashboardVm.GpuFanRpm,
            gpuName = _dashboardVm.GpuName,
            currentMode = _dashboardVm.CurrentModeName,
            isLimited = _dashboardVm.IsLimitedMode,
            lastError = _dashboardVm.LastError
        };

        return JsonSerializer.Serialize(stats);
    }

    public string GetProcesses()
    {
        try
        {
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                .Select(p =>
                {
                    try
                    {
                        return new
                        {
                            name = p.ProcessName,
                            memMb = Math.Round(p.WorkingSet64 / 1024.0 / 1024.0, 1),
                            id = p.Id,
                            title = string.IsNullOrWhiteSpace(p.MainWindowTitle) ? null : p.MainWindowTitle
                        };
                    }
                    catch { return null; }
                })
                .Where(p => p != null && p.memMb > 5)
                .OrderByDescending(p => p!.memMb)
                .Take(25)
                .ToList();

            var sysInfo = new
            {
                osVersion = Environment.OSVersion.ToString(),
                cpuCores = Environment.ProcessorCount,
                machineName = Environment.MachineName,
                uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm\:ss"),
                processes,
                totalProcessCount = Process.GetProcesses().Length
            };

            return JsonSerializer.Serialize(sysInfo);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    // ── OC Controls ──
    
    public string GetOcInfo()
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        var info = new
        {
            gpuName = gpu.GpuName,
            supported = gpu.IsSupported,
            powerLimitW = gpu.CurrentPowerLimitWatts,
            defaultPowerW = gpu.DefaultPowerLimitWatts,
            minPowerW = gpu.MinPowerLimitWatts,
            maxPowerW = gpu.MaxPowerLimitWatts,
            currentCoreMHz = gpu.CurrentGpuClockMHz,
            currentMemMHz = gpu.CurrentMemClockMHz,
            maxCoreMHz = gpu.MaxSupportedGpuClockMHz,
            maxMemMHz = gpu.MaxSupportedMemClockMHz,
            maxCoreOcOffset = 100,
            maxMemOcOffset = 300
        };
        return JsonSerializer.Serialize(info);
    }

    public string ApplyGpuCoreClock(int mhz)
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        bool ok = gpu.LockGpuClocks(mhz);
        return JsonSerializer.Serialize(new { success = ok, message = ok ? $"GPU core locked to {mhz}MHz" : "Failed to set GPU core clock" });
    }

    public string ApplyGpuMemClock(int mhz)
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        bool ok = gpu.LockMemoryClocks(mhz);
        return JsonSerializer.Serialize(new { success = ok, message = ok ? $"GPU memory locked to {mhz}MHz" : "Failed to set GPU memory clock" });
    }

    public string ApplyGpuPowerLimit(float watts)
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        bool ok = gpu.SetPowerLimit(watts);
        return JsonSerializer.Serialize(new { success = ok, message = ok ? $"GPU power limit set to {watts}W" : "Failed to set power limit" });
    }

    public string ResetAllOc()
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        gpu.ResetGpuClocks();
        gpu.ResetMemoryClocks();
        gpu.RestoreDefaultPowerLimit();
        return JsonSerializer.Serialize(new { success = true, message = "All GPU overclocks reset to defaults" });
    }

    public void StartOcScan()
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        gpu.StartOcScan();
    }

    public void CancelOcScan()
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        gpu.CancelOcScan();
    }

    public string GetOcScanStatus()
    {
        var gpu = App.ServiceProvider.GetRequiredService<IGpuControlService>();
        var status = new
        {
            isScanning = gpu.IsOcScanning,
            progress = gpu.OcScanProgressPercent,
            message = gpu.OcScanStatusMessage,
            currentMHz = gpu.OcScanCurrentTestMHz
        };
        return JsonSerializer.Serialize(status);
    }

    public string ApplyCustomFanCurve(string jsonCurveData)
    {
        try
        {
            var fan = App.ServiceProvider.GetRequiredService<IFanBridgeService>();
            bool ok = fan.ApplyCustomCurve(jsonCurveData);
            return JsonSerializer.Serialize(new { success = ok, message = ok ? "Custom fan curve applied successfully" : "Failed to apply fan curve (ensure FanControl is installed)" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = $"Failed to save fan curve: {ex.Message}" });
        }
    }

    public bool SetAutoSwitching(bool enabled)
    {
        var svc = App.ServiceProvider.GetRequiredService<IGameDetectionService>();
        svc.IsAutoSwitchingEnabled = enabled;
        return enabled;
    }

    public bool GetAutoSwitching()
    {
        var svc = App.ServiceProvider.GetRequiredService<IGameDetectionService>();
        return svc.IsAutoSwitchingEnabled;
    }

    // ── GPU Stress Test ──

    public void StartStressTest(int durationSeconds, int maxTempLimitC)
    {
        var svc = App.ServiceProvider.GetRequiredService<IGpuStressTestService>();
        svc.StartStressTest(durationSeconds, maxTempLimitC);
    }

    public void CancelStressTest()
    {
        var svc = App.ServiceProvider.GetRequiredService<IGpuStressTestService>();
        svc.CancelStressTest();
    }

    public string GetStressTestStatus()
    {
        var svc = App.ServiceProvider.GetRequiredService<IGpuStressTestService>();
        var status = new
        {
            isStressing = svc.IsStressing,
            progress = svc.ProgressPercent,
            message = svc.StatusMessage,
            maxGpuTemp = svc.MaxGpuTempRecorded,
            minGpuTemp = svc.MinGpuTempRecorded,
            avgGpuTemp = svc.AvgGpuTempRecorded,
            maxGpuPower = svc.MaxGpuPowerRecorded,
            avgGpuPower = svc.AvgGpuPowerRecorded,
            maxGpuClock = svc.MaxGpuClockRecorded,
            avgGpuClock = svc.AvgGpuClockRecorded,
            maxGpuFanRpm = svc.MaxGpuFanRpmRecorded,
            maxVramMb = svc.MaxVramUsageMb,
            throttleEvents = svc.ThrottleEventCount,
            stabilityGrade = svc.StabilityGrade,
            rigScore = svc.LastRigScore
        };
        return JsonSerializer.Serialize(status);
    }

    // ── CPU Stress Test ──

    public void StartCpuStressTest(int durationSeconds, int maxTempLimitC)
    {
        var svc = App.ServiceProvider.GetRequiredService<ICpuStressTestService>();
        svc.StartStressTest(durationSeconds, maxTempLimitC);
    }

    public void CancelCpuStressTest()
    {
        var svc = App.ServiceProvider.GetRequiredService<ICpuStressTestService>();
        svc.CancelStressTest();
    }

    public string GetCpuStressTestStatus()
    {
        var svc = App.ServiceProvider.GetRequiredService<ICpuStressTestService>();
        var status = new
        {
            isStressing = svc.IsStressing,
            progress = svc.ProgressPercent,
            message = svc.StatusMessage,
            maxCpuTemp = svc.MaxCpuTempRecorded,
            minCpuTemp = svc.MinCpuTempRecorded,
            avgCpuTemp = svc.AvgCpuTempRecorded,
            maxCpuPower = svc.MaxCpuPowerRecorded,
            avgCpuPower = svc.AvgCpuPowerRecorded,
            maxCpuClock = svc.MaxCpuClockRecorded,
            avgCpuClock = svc.AvgCpuClockRecorded,
            throttleEvents = svc.ThrottleEventCount,
            stabilityGrade = svc.StabilityGrade,
            cpuScore = svc.LastCpuScore
        };
        return JsonSerializer.Serialize(status);
    }

    // ── Combined Stress Test (CPU + GPU simultaneously) ──

    public void StartCombinedStressTest(int durationSeconds, int maxTempLimitC)
    {
        var gpuSvc = App.ServiceProvider.GetRequiredService<IGpuStressTestService>();
        var cpuSvc = App.ServiceProvider.GetRequiredService<ICpuStressTestService>();
        gpuSvc.StartStressTest(durationSeconds, maxTempLimitC);
        cpuSvc.StartStressTest(durationSeconds, maxTempLimitC);
    }

    public void CancelCombinedStressTest()
    {
        var gpuSvc = App.ServiceProvider.GetRequiredService<IGpuStressTestService>();
        var cpuSvc = App.ServiceProvider.GetRequiredService<ICpuStressTestService>();
        gpuSvc.CancelStressTest();
        cpuSvc.CancelStressTest();
    }

    public string GetCombinedStressTestStatus()
    {
        var gpuSvc = App.ServiceProvider.GetRequiredService<IGpuStressTestService>();
        var cpuSvc = App.ServiceProvider.GetRequiredService<ICpuStressTestService>();

        bool eitherRunning = gpuSvc.IsStressing || cpuSvc.IsStressing;
        int combinedProgress = eitherRunning
            ? (gpuSvc.ProgressPercent + cpuSvc.ProgressPercent) / 2
            : Math.Max(gpuSvc.ProgressPercent, cpuSvc.ProgressPercent);

        // Combined RIG score = weighted sum of both
        int? combinedScore = null;
        if (gpuSvc.LastRigScore.HasValue && cpuSvc.LastCpuScore.HasValue)
            combinedScore = (int)(gpuSvc.LastRigScore.Value * 0.6f + cpuSvc.LastCpuScore.Value * 0.4f);

        string combinedGrade = "?";
        if (gpuSvc.StabilityGrade != null && cpuSvc.StabilityGrade != null)
            combinedGrade = CombineGrades(gpuSvc.StabilityGrade, cpuSvc.StabilityGrade);

        var status = new
        {
            isStressing = eitherRunning,
            progress = combinedProgress,
            gpuMessage = gpuSvc.StatusMessage,
            cpuMessage = cpuSvc.StatusMessage,
            // GPU telemetry
            maxGpuTemp = gpuSvc.MaxGpuTempRecorded,
            avgGpuTemp = gpuSvc.AvgGpuTempRecorded,
            maxGpuClock = gpuSvc.MaxGpuClockRecorded,
            maxGpuPower = gpuSvc.MaxGpuPowerRecorded,
            gpuThrottleEvents = gpuSvc.ThrottleEventCount,
            gpuGrade = gpuSvc.StabilityGrade,
            rigScore = gpuSvc.LastRigScore,
            // CPU telemetry
            maxCpuTemp = cpuSvc.MaxCpuTempRecorded,
            avgCpuTemp = cpuSvc.AvgCpuTempRecorded,
            maxCpuClock = cpuSvc.MaxCpuClockRecorded,
            maxCpuPower = cpuSvc.MaxCpuPowerRecorded,
            cpuThrottleEvents = cpuSvc.ThrottleEventCount,
            cpuGrade = cpuSvc.StabilityGrade,
            cpuScore = cpuSvc.LastCpuScore,
            // Combined
            combinedScore,
            combinedGrade
        };
        return JsonSerializer.Serialize(status);
    }

    private static string CombineGrades(string g1, string g2)
    {
        static int GradeToNum(string g) => g switch
        {
            "A+" => 7, "A" => 6, "B+" => 5, "B" => 4,
            "C" => 3, "D" => 2, "F" => 1, _ => 0
        };
        static string NumToGrade(int n) => n switch
        {
            >= 7 => "A+", 6 => "A", 5 => "B+", 4 => "B",
            3 => "C", 2 => "D", _ => "F"
        };
        int avg = (GradeToNum(g1) + GradeToNum(g2)) / 2;
        return NumToGrade(avg);
    }


    public string KillProcess(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            var name = process.ProcessName;
            process.Kill();
            process.WaitForExit(3000);
            return JsonSerializer.Serialize(new { success = true, message = $"Closed {name} (PID {pid})" });
        }
        catch (ArgumentException)
        {
            return JsonSerializer.Serialize(new { success = false, message = $"Process {pid} not found (already closed?)" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, message = $"Failed to close PID {pid}: {ex.Message}" });
        }
    }

    public void SetMode(string modeName)
    {
        if (Enum.TryParse<PerformanceMode>(modeName, true, out var mode))
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _dashboardVm.SetMode(mode);
            });
        }
    }

    public string GetProfiles()
    {
        var svc = App.ServiceProvider.GetRequiredService<IProfileManagerService>();
        return JsonSerializer.Serialize(svc.GetProfiles());
    }
    
    public bool SaveProfile(string id, string name, string coreStr, string memStr, string pwrStr, string fanJson)
    {
        var svc = App.ServiceProvider.GetRequiredService<IProfileManagerService>();
        
        int? core = int.TryParse(coreStr, out int c) ? c : null;
        int? mem = int.TryParse(memStr, out int m) ? m : null;
        float? pwr = float.TryParse(pwrStr, out float p) ? p : null;
        
        var profile = new GpuProfile
        {
            Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id,
            Name = name,
            CoreOffsetMhz = core,
            MemOffsetMhz = mem,
            PowerLimitWatts = pwr,
            FanCurveJson = string.IsNullOrEmpty(fanJson) ? null : fanJson
        };
        svc.SaveProfile(profile);
        return true;
    }
    
    public bool LoadProfile(string id)
    {
        var svc = App.ServiceProvider.GetRequiredService<IProfileManagerService>();
        return svc.ApplyProfile(id);
    }
    
    public bool DeleteProfile(string id)
    {
        var svc = App.ServiceProvider.GetRequiredService<IProfileManagerService>();
        svc.DeleteProfile(id);
        return true;
    }
}
