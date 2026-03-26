using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;

namespace RogCustom.Hardware;

/// <summary>
/// Single LHM Computer, single worker thread. Driver-safe Open; Interlocked.Exchange for snapshot.
/// RequestRebind is a flag handled by the worker; UI never touches LHM.
/// </summary>
public sealed class HardwareMonitor : IHardwareMonitor, IDisposable
{
    private readonly ILogger<HardwareMonitor> _logger;
    private readonly SensorBindingLayer _bindingLayer;
    private readonly LogThrottle _logThrottle;
    private readonly IAppCapabilitiesService _capabilities;
    private readonly Computer _computer;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _worker;
    private HardwareSnapshot _lastSnapshot = HardwareSnapshot.Empty;
    private DiagnosticsSnapshot _lastDiagnostics = DiagnosticsSnapshot.Empty;
    private volatile int _rebindRequested;
    private volatile int _diagnosticsRefreshRequested;
    private bool _isLimitedMode;
    private bool _disposed;

    private PerformanceCounter? _cpuTotalCounter;
    private PerformanceCounter? _memAvailableBytesCounter;
    private DateTimeOffset _lastCpuPerfCounterRead;
    private TimeSpan _lastCpuTotal;
    private DateTimeOffset _lastCpuSampleTime;

    public bool IsLimitedMode => _isLimitedMode;

    public HardwareMonitor(ILogger<HardwareMonitor> logger, SensorBindingLayer bindingLayer, IAppCapabilitiesService capabilities)
    {
        _logger = logger;
        _bindingLayer = bindingLayer;
        _capabilities = capabilities;
        _logThrottle = new LogThrottle(TimeSpan.FromSeconds(10)); // Throttle recurring errors to once per 10 seconds
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = false,
        };
        _worker = new Thread(WorkerLoop) { IsBackground = true };
        _worker.Start();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private float? TryGetCpuUsagePercent()
    {
        try
        {
            // Prefer system-wide CPU usage counter if available.
            _cpuTotalCounter ??= new PerformanceCounter("Processor", "% Processor Time", "_Total", true);

            // First read of PerformanceCounter is often 0; require a little time between reads.
            var now = DateTimeOffset.UtcNow;
            if (_lastCpuPerfCounterRead == default)
            {
                _lastCpuPerfCounterRead = now;
                _ = _cpuTotalCounter.NextValue();
                return null;
            }
            if ((now - _lastCpuPerfCounterRead).TotalMilliseconds < 250)
                return null;

            _lastCpuPerfCounterRead = now;
            var v = _cpuTotalCounter.NextValue();
            if (v < 0 || float.IsNaN(v) || float.IsInfinity(v))
                return null;
            return Math.Clamp(v, 0f, 100f);
        }
        catch
        {
            // Fallback: approximate using process CPU time (less accurate than system-wide CPU %).
            var now = DateTimeOffset.UtcNow;
            if (_lastCpuSampleTime == default)
            {
                _lastCpuSampleTime = now;
                _lastCpuTotal = Environment.ProcessId >= 0 ? Process.GetCurrentProcess().TotalProcessorTime : TimeSpan.Zero;
                return null;
            }

            var proc = Process.GetCurrentProcess();
            var total = proc.TotalProcessorTime;
            var totalDelta = total - _lastCpuTotal;
            var wallDelta = now - _lastCpuSampleTime;
            if (wallDelta.TotalMilliseconds <= 0)
                return null;

            _lastCpuTotal = total;
            _lastCpuSampleTime = now;

            var usage = totalDelta.TotalMilliseconds / (wallDelta.TotalMilliseconds * Environment.ProcessorCount) * 100.0;
            if (double.IsNaN(usage) || double.IsInfinity(usage))
                return null;

            return (float)Math.Clamp(usage, 0.0, 100.0);
        }
    }

    private (float? usedMb, float? totalMb) TryGetRamMb()
    {
        try
        {
            // Try perf counter for available bytes (cheap, no WMI).
            _memAvailableBytesCounter ??= new PerformanceCounter("Memory", "Available Bytes", readOnly: true);
            var availBytes = (double)_memAvailableBytesCounter.NextValue();

            var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref ms) || ms.ullTotalPhys == 0)
                return (null, null);

            var totalBytes = (double)ms.ullTotalPhys;
            if (availBytes <= 0)
                availBytes = (double)ms.ullAvailPhys;

            var usedBytes = Math.Max(0, totalBytes - availBytes);
            return (
                (float)(usedBytes / 1024.0 / 1024.0),
                (float)(totalBytes / 1024.0 / 1024.0));
        }
        catch
        {
            return (null, null);
        }
    }

    public HardwareSnapshot GetLastSnapshot()
    {
        return Volatile.Read(ref _lastSnapshot) ?? HardwareSnapshot.Empty;
    }

    public DiagnosticsSnapshot GetDiagnosticsSnapshot()
    {
        return Volatile.Read(ref _lastDiagnostics) ?? DiagnosticsSnapshot.Empty;
    }

    public void RequestRebind()
    {
        Interlocked.Exchange(ref _rebindRequested, 1);
    }

    public void RequestDiagnosticsRefresh()
    {
        Interlocked.Exchange(ref _diagnosticsRefreshRequested, 1);
    }

    private void WorkerLoop()
    {
        try
        {
            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LibreHardwareMonitor driver failed to load (e.g. Anti-Cheat, Secure Boot). Running in Limited Mode (Power Plan only).");
                _isLimitedMode = true;
                _capabilities.SetMonitorAvailable(false); // Update monitor availability
                _capabilities.SetLastError("Hardware monitor driver blocked or unavailable (Anti-Cheat/Secure Boot)");
                // Keep writing empty snapshot so UI doesn't hang
                while (!_cts.Token.IsCancellationRequested)
                {
                    var cpuUsage = TryGetCpuUsagePercent();
                    var (ramUsedMb, ramTotalMb) = TryGetRamMb();
                    var empty = new HardwareSnapshot(null, null, null, null, null, null, null, cpuUsage, ramUsedMb, ramTotalMb, null, null, null, null, null, null, DateTimeOffset.UtcNow);
                    Interlocked.Exchange(ref _lastSnapshot, empty);
                    Thread.Sleep(500);
                }
                return;
            }

            // LHM often needs at least one successful Update() pass before sensors populate.
            // If we bind too early, we can lock in null/irrelevant sensors until restart.
            WarmUpAndUpdateHardware();
            _bindingLayer.Resolve(_computer);

            var goldenBindings = _bindingLayer.GetBindingSummary();
            _logger.LogInformation("GOLDEN snapshot: {Bindings}",
                string.Join(", ", goldenBindings.Select(b => $"{b.Key}={b.Value?.SensorName ?? "UNBOUND"}({b.Value?.Status})")));

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var rebind = Interlocked.Exchange(ref _rebindRequested, 0) == 1;
                    var refreshDiag = Interlocked.Exchange(ref _diagnosticsRefreshRequested, 0) == 1;

                    UpdateHardwareTree();

                    // Rebind after updates (ensures we bind against sensors that have values).
                    if (rebind)
                        _bindingLayer.Resolve(_computer);

                    if (refreshDiag)
                    {
                        var diag = BuildDiagnosticsSnapshot();
                        Interlocked.Exchange(ref _lastDiagnostics, diag);
                    }

                    var cpuTemp = _bindingLayer.GetValue(DesiredSensorRole.CpuPackageTemp);
                    var gpuTemp = _bindingLayer.GetValue(DesiredSensorRole.GpuCoreTemp);
                    var cpuFan = _bindingLayer.GetValue(DesiredSensorRole.CpuFan);
                    var gpuFan = _bindingLayer.GetValue(DesiredSensorRole.GpuFan);
                    var gpuCoreClock = _bindingLayer.GetValue(DesiredSensorRole.GpuCoreClockMHz);
                    var gpuMemClock = _bindingLayer.GetValue(DesiredSensorRole.GpuMemoryClockMHz);
                    var cpuPower = _bindingLayer.GetValue(DesiredSensorRole.CpuPowerWatts);
                    var gpuPower = _bindingLayer.GetValue(DesiredSensorRole.GpuPowerWatts);
                    var gpuUsage = _bindingLayer.GetValue(DesiredSensorRole.GpuUsagePercent);
                    var gpuVramUsed = _bindingLayer.GetValue(DesiredSensorRole.GpuVramUsedMb);
                    var gpuVramTotal = _bindingLayer.GetValue(DesiredSensorRole.GpuVramTotalMb);
                    var cpuClock = _bindingLayer.GetValue(DesiredSensorRole.CpuEffectiveClockMHz);
                    var gpuName = _computer.Hardware.FirstOrDefault(h =>
                            h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel)
                        ?.Name;

                    var cpuUsage = TryGetCpuUsagePercent();
                    var (ramUsedMb, ramTotalMb) = TryGetRamMb();

                    var snapshot = new HardwareSnapshot(
                        cpuTemp,
                        gpuTemp,
                        cpuFan,
                        gpuFan,
                        gpuName,
                        gpuCoreClock,
                        gpuMemClock,
                        cpuUsage,
                        ramUsedMb,
                        ramTotalMb,
                        cpuPower,
                        gpuPower,
                        gpuUsage,
                        gpuVramUsed,
                        gpuVramTotal,
                        cpuClock,
                        DateTimeOffset.UtcNow);
                    Interlocked.Exchange(ref _lastSnapshot, snapshot);
                }
                catch (Exception ex)
                {
                    if (_logThrottle.ShouldLog("WorkerLoopError"))
                        _logger.LogDebug(ex, "Worker loop iteration error (throttled)");
                }

                Thread.Sleep(500);
            }
        }
        finally
        {
            try
            {
                _computer.Close();
            }
            catch { /* ignore */ }
        }
    }

    private void WarmUpAndUpdateHardware()
    {
        try
        {
            // A couple of passes gives LHM time to populate values.
            for (int i = 0; i < 3; i++)
            {
                UpdateHardwareTree();
                Thread.Sleep(200);
            }
        }
        catch (Exception ex)
        {
            if (_logThrottle.ShouldLog("WarmupError"))
                _logger.LogDebug(ex, "Warmup update failed (throttled)");
        }
    }

    private void UpdateHardwareTree()
    {
        foreach (var hardware in _computer.Hardware)
            UpdateHardwareRecursive(hardware);
    }

    private static void UpdateHardwareRecursive(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            UpdateHardwareRecursive(sub);
    }

    private DiagnosticsSnapshot BuildDiagnosticsSnapshot()
    {
        try
        {
            var hw = _computer.Hardware.Select(BuildDiagnosticsHardware).ToList();
            var bindings = _bindingLayer.GetBindingSummary();
            return new DiagnosticsSnapshot(DateTimeOffset.UtcNow, _isLimitedMode, _capabilities.LastError, hw, bindings);
        }
        catch (Exception ex)
        {
            if (_logThrottle.ShouldLog("DiagnosticsBuildError"))
                _logger.LogDebug(ex, "Failed to build diagnostics snapshot (throttled)");
            return new DiagnosticsSnapshot(DateTimeOffset.UtcNow, _isLimitedMode, _capabilities.LastError, Array.Empty<DiagnosticsHardware>(), _bindingLayer.GetBindingSummary());
        }
    }

    private static DiagnosticsHardware BuildDiagnosticsHardware(IHardware hardware)
    {
        var sensors = hardware.Sensors
            .Select(s => new DiagnosticsSensor(s.SensorType.ToString(), s.Name ?? "", s.Value))
            .ToList();
        var subs = hardware.SubHardware.Select(BuildDiagnosticsHardware).ToList();
        return new DiagnosticsHardware(hardware.HardwareType.ToString(), hardware.Name ?? "", sensors, subs);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cts.Cancel();
        if (_worker.IsAlive && !_worker.Join(TimeSpan.FromSeconds(2)))
        {
            // Abandon worker to prevent UI hang; thread will eventually exit when process ends
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
