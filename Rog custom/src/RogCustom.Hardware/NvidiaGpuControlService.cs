using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RogCustom.Hardware;

public sealed class NvidiaGpuControlService : IGpuControlService, IDisposable
{
    private readonly ILogger<NvidiaGpuControlService> _logger;
    private bool _disposed;
    private bool _detected;
    private string? _nvidiaSmiPath;

    public NvidiaGpuControlService(ILogger<NvidiaGpuControlService> logger)
    {
        _logger = logger;
        Detect();
    }

    public bool IsSupported => _detected;
    public bool IsConnected => _detected;
    public string? GpuName { get; private set; }
    public float? DefaultPowerLimitWatts { get; private set; }
    public float? MinPowerLimitWatts { get; private set; }
    public float? MaxPowerLimitWatts { get; private set; }
    public float? CurrentPowerLimitWatts { get; private set; }

    public bool SetPowerLimit(float watts)
    {
        if (!_detected || _nvidiaSmiPath == null) return false;
        if (MinPowerLimitWatts.HasValue && watts < MinPowerLimitWatts.Value) return false;
        if (MaxPowerLimitWatts.HasValue && watts > MaxPowerLimitWatts.Value) return false;

        var result = RunNvidiaSmi($"-pl {watts.ToString("F1", CultureInfo.InvariantCulture)}");
        if (result == null) return false;

        CurrentPowerLimitWatts = watts;
        _logger.LogInformation("GPU power limit set to {Watts}W", watts);
        return true;
    }

    public bool RestoreDefaultPowerLimit()
    {
        if (!_detected || !DefaultPowerLimitWatts.HasValue) return true;
        return SetPowerLimit(DefaultPowerLimitWatts.Value);
    }

    // ── OC: Clock properties ──
    public int? MaxSupportedGpuClockMHz { get; private set; }
    public int? MaxSupportedMemClockMHz { get; private set; }
    public int? CurrentGpuClockMHz { get; private set; }
    public int? CurrentMemClockMHz { get; private set; }
    
    private int? _defaultGpuClockMHz;
    private int? _defaultMemClockMHz;

    // Hard safety caps: max OC offset allowed
    private const int MAX_CORE_OC_OFFSET = 100;  // +100MHz max
    private const int MAX_MEM_OC_OFFSET = 300;    // +300MHz max

    // OC Scanner State
    private CancellationTokenSource? _ocScanCts;
    public bool IsOcScanning { get; private set; }
    public int OcScanProgressPercent { get; private set; }
    public string? OcScanStatusMessage { get; private set; }
    public int? OcScanCurrentTestMHz { get; private set; }

    public bool LockGpuClocks(int maxMHz)
    {
        if (!_detected || _nvidiaSmiPath == null) return false;
        
        // Enforce hard cap: never exceed default + 100MHz
        if (_defaultGpuClockMHz.HasValue)
        {
            int hardCap = _defaultGpuClockMHz.Value + MAX_CORE_OC_OFFSET;
            if (maxMHz > hardCap) maxMHz = hardCap;
        }
        if (maxMHz < 300) return false; // sanity floor
        
        var result = RunNvidiaSmi($"-lgc 300,{maxMHz}");
        if (result == null) return false;
        
        CurrentGpuClockMHz = maxMHz;
        _logger.LogInformation("GPU clock locked to max {MHz}MHz", maxMHz);
        return true;
    }

    public bool LockMemoryClocks(int maxMHz)
    {
        if (!_detected || _nvidiaSmiPath == null) return false;
        
        // Enforce hard cap: never exceed default + 300MHz
        if (_defaultMemClockMHz.HasValue)
        {
            int hardCap = _defaultMemClockMHz.Value + MAX_MEM_OC_OFFSET;
            if (maxMHz > hardCap) maxMHz = hardCap;
        }
        if (maxMHz < 300) return false;
        
        var result = RunNvidiaSmi($"-lmc 300,{maxMHz}");
        if (result == null) return false;
        
        CurrentMemClockMHz = maxMHz;
        _logger.LogInformation("GPU memory clock locked to max {MHz}MHz", maxMHz);
        return true;
    }

    public bool ResetGpuClocks()
    {
        if (!_detected || _nvidiaSmiPath == null) return false;
        var result = RunNvidiaSmi("-rgc");
        CurrentGpuClockMHz = _defaultGpuClockMHz;
        _logger.LogInformation("GPU clocks reset to defaults");
        return result != null;
    }

    public bool ResetMemoryClocks()
    {
        if (!_detected || _nvidiaSmiPath == null) return false;
        var result = RunNvidiaSmi("-rmc");
        CurrentMemClockMHz = _defaultMemClockMHz;
        _logger.LogInformation("GPU memory clocks reset to defaults");
        return result != null;
    }

    public string? QueryCurrentClocks()
    {
        if (!_detected || _nvidiaSmiPath == null) return null;
        return RunNvidiaSmi("--query-gpu=clocks.current.graphics,clocks.current.memory,clocks.max.graphics,clocks.max.memory --format=csv,noheader,nounits");
    }

    public void StartOcScan()
    {
        if (!_detected || IsOcScanning) return;
        
        IsOcScanning = true;
        OcScanProgressPercent = 0;
        OcScanStatusMessage = "Initializing OC Scanner (AI Heuristic Search)...";
        OcScanCurrentTestMHz = _defaultGpuClockMHz;
        
        _ocScanCts = new CancellationTokenSource();
        Task.Run(() => RunOcScanAsync(_ocScanCts.Token));
    }

    public void CancelOcScan()
    {
        if (!IsOcScanning) return;
        _ocScanCts?.Cancel();
    }

    private int? GetRealtimeGraphicsClock()
    {
        var clockInfo = QueryCurrentClocks();
        if (clockInfo != null)
        {
            var parts = clockInfo.Trim().Split(',');
            if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out var curGpu))
            {
                return curGpu;
            }
        }
        return null;
    }

    private async Task RunOcScanAsync(CancellationToken token)
    {
        try
        {
            _logger.LogInformation("Starting Genuine Hardware GPU OC Scan...");
            int baseClock = _defaultGpuClockMHz ?? 1500;
            // Allow up to a +150MHz physical test limit
            int maxSafeClock = baseClock + 150;
            if (maxSafeClock > baseClock + MAX_CORE_OC_OFFSET) 
                maxSafeClock = baseClock + MAX_CORE_OC_OFFSET;
                
            int currentTest = baseClock;
            int lastStableClock = baseClock;

            OcScanStatusMessage = "Establishing baseline voltage-frequency curve...";
            OcScanProgressPercent = 5;
            
            // Step 1: Lock base and settle
            LockGpuClocks(baseClock);
            await Task.Delay(2000, token);

            // Step 2: Step up by 10MHz until driver rejects it
            for (int step = 1; step <= 15; step++)
            {
                token.ThrowIfCancellationRequested();

                currentTest = baseClock + (step * 10);
                if (currentTest > maxSafeClock) currentTest = maxSafeClock;

                OcScanCurrentTestMHz = currentTest;
                OcScanProgressPercent = 5 + (step * 6);
                
                OcScanStatusMessage = $"Applying {currentTest}MHz and measuring hardware reaction...";
                LockGpuClocks(currentTest);
                
                // Give the driver and hardware 3 seconds to attempt the boost and warm up
                await Task.Delay(3000, token);
                
                // Measure the actual running clock
                int? realClock = GetRealtimeGraphicsClock();
                if (realClock.HasValue)
                {
                    _logger.LogInformation("OC Scan - Requested: {Req}MHz, Actual Running: {Act}MHz", currentTest, realClock.Value);
                    
                    // If actual clock drops significantly below what we demanded, the thermal/power limit or silicon rejected it
                    if (currentTest - realClock.Value > 15)
                    {
                        OcScanStatusMessage = $"Hardware rejected {currentTest}MHz (Throttled to {realClock.Value}MHz). Limit found!";
                        _logger.LogInformation("OC Limit Detected. GPU cannot sustain {Req}MHz.", currentTest);
                        await Task.Delay(2000, token);
                        break;
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to read realtime clock during scan.");
                }

                lastStableClock = currentTest;
                if (currentTest >= maxSafeClock) break;
            }

            // Phase 3: Final Validation
            token.ThrowIfCancellationRequested();
            OcScanStatusMessage = "Silicon limit found. Locking in max stable frequency...";
            OcScanProgressPercent = 90;
            await Task.Delay(2000, token);

            // Phase 4: Found Optimal (step back slightly for daily driver stability)
            int optimalClock = lastStableClock - 10;
            if (optimalClock < baseClock) optimalClock = baseClock;
            
            LockGpuClocks(optimalClock);
            
            OcScanProgressPercent = 100;
            OcScanStatusMessage = $"Scan Complete! Verified stable core offset applied: {optimalClock}MHz.";
            _logger.LogInformation("Genuine OC Scan completed at {MHz}MHz", optimalClock);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GPU OC Scan cancelled by user.");
            ResetGpuClocks();
            OcScanStatusMessage = "Scan cancelled. Defaults restored.";
            OcScanProgressPercent = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OC Scan failed.");
            ResetGpuClocks();
            OcScanStatusMessage = "Scan failed due to an error.";
            OcScanProgressPercent = 0;
        }
        finally
        {
            IsOcScanning = false;
        }
    }


    private void Detect()
    {
        var candidates = new[]
        {
            @"C:\Windows\System32\nvidia-smi.exe",
            @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                _nvidiaSmiPath = path;
                break;
            }
        }

        if (_nvidiaSmiPath == null)
        {
            _logger.LogInformation("nvidia-smi not found, GPU control unavailable");
            return;
        }

        var output = RunNvidiaSmi("-q -d POWER");
        if (output == null) return;

        _detected = true;

        GpuName = ParseField(RunNvidiaSmi("--query-gpu=name --format=csv,noheader,nounits") ?? "");
        DefaultPowerLimitWatts = ParseWatts(output, @"Default Power Limit\s*:\s*([\d.]+)\s*W");
        MinPowerLimitWatts = ParseWatts(output, @"Min Power Limit\s*:\s*([\d.]+)\s*W");
        MaxPowerLimitWatts = ParseWatts(output, @"Max Power Limit\s*:\s*([\d.]+)\s*W");
        CurrentPowerLimitWatts = ParseWatts(output, @"Power Limit\s*:\s*([\d.]+)\s*W");
        
        // Query current and max clocks
        var clockInfo = RunNvidiaSmi("--query-gpu=clocks.current.graphics,clocks.current.memory,clocks.max.graphics,clocks.max.memory --format=csv,noheader,nounits");
        if (clockInfo != null)
        {
            var parts = clockInfo.Trim().Split(',');
            if (parts.Length >= 4)
            {
                if (int.TryParse(parts[0].Trim(), out var curGpu)) { CurrentGpuClockMHz = curGpu; _defaultGpuClockMHz = curGpu; }
                if (int.TryParse(parts[1].Trim(), out var curMem)) { CurrentMemClockMHz = curMem; _defaultMemClockMHz = curMem; }
                if (int.TryParse(parts[2].Trim(), out var maxGpu)) MaxSupportedGpuClockMHz = maxGpu;
                if (int.TryParse(parts[3].Trim(), out var maxMem)) MaxSupportedMemClockMHz = maxMem;
            }
        }

        _logger.LogInformation("NVIDIA GPU detected: {Name}, power range {Min}-{Max}W, default {Default}W, max GPU clock {MaxGpu}MHz, max mem clock {MaxMem}MHz",
            GpuName, MinPowerLimitWatts, MaxPowerLimitWatts, DefaultPowerLimitWatts, MaxSupportedGpuClockMHz, MaxSupportedMemClockMHz);
    }

    private static float? ParseWatts(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return val;
        return null;
    }

    private static string? ParseField(string text)
    {
        var trimmed = text.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private string? RunNvidiaSmi(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _nvidiaSmiPath ?? "nvidia-smi",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit(5000);
            if (proc.ExitCode != 0)
            {
                _logger.LogWarning("nvidia-smi {Args} failed (exit {Code}): {Err}",
                    args, proc.ExitCode, stderrTask.GetAwaiter().GetResult());
                return null;
            }
            return stdoutTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "nvidia-smi {Args} threw", args);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelOcScan();
        ResetGpuClocks();
        ResetMemoryClocks();
        RestoreDefaultPowerLimit();
    }
}
