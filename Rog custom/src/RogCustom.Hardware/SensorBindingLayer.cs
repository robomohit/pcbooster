using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;

namespace RogCustom.Hardware;

/// <summary>
/// Maps DesiredSensorRole to LHM sensors. Re-resolution is triggered by RequestRebind (handled on worker thread).
/// Only used on the worker thread; holds references to LHM sensor objects.
/// </summary>
public sealed class SensorBindingLayer
{
    private readonly ILogger<SensorBindingLayer> _logger;
    private readonly Dictionary<DesiredSensorRole, ISensor?> _bindings = new();
    private readonly Dictionary<DesiredSensorRole, (ISensor Sensor, IHardware Hardware, int Score)> _candidates = new();
    private readonly Dictionary<DesiredSensorRole, BoundSensor?> _boundInfo = new();
    private readonly Dictionary<DesiredSensorRole, int> _boundScores = new();
    private readonly object _bindingsLock = new();

    public SensorBindingLayer(ILogger<SensorBindingLayer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Re-resolve role -> sensor from the given computer. Call only on the worker thread.
    /// </summary>
    public void Resolve(Computer computer)
    {
        lock (_bindingsLock)
        {
            _candidates.Clear();
            _logger.LogDebug("Starting sensor resolution for {HardwareCount} hardware items", computer.Hardware.Count);
            foreach (var hardware in computer.Hardware)
                TryBindFromHardware(hardware);

            foreach (DesiredSensorRole role in Enum.GetValues(typeof(DesiredSensorRole)))
            {
                if (!_candidates.TryGetValue(role, out var cand))
                    continue;

                // Prefer:
                // 1) A candidate that currently has a value
                // 2) Higher score
                // But do not replace a previously-bound sensor unless the new one is better.
                var candScore = cand.Score;
                if (cand.Sensor.Value.HasValue)
                    candScore += 25;

                if (_boundScores.TryGetValue(role, out var existingScore) && _bindings.TryGetValue(role, out var existingSensor) && existingSensor != null)
                {
                    var existingEffective = existingScore;
                    if (existingSensor.Value.HasValue)
                        existingEffective += 25;

                    if (candScore <= existingEffective)
                        continue;
                }

                _bindings[role] = cand.Sensor;
                _boundScores[role] = cand.Score;
                var status = cand.Sensor.Value.HasValue ? SensorStatus.BoundHasValue : SensorStatus.BoundNoValue;
                _boundInfo[role] = new BoundSensor(
                    cand.Hardware.HardwareType.ToString(),
                    cand.Hardware.Name ?? "",
                    cand.Sensor.SensorType.ToString(),
                    cand.Sensor.Name ?? "",
                    status);
            }
            
            _logger.LogInformation("Sensor binding complete: CPU Temp: {CpuBound}, GPU Temp: {GpuBound}, CPU Fan: {CpuFanBound}, GPU Fan: {GpuFanBound}",
                _bindings.ContainsKey(DesiredSensorRole.CpuPackageTemp),
                _bindings.ContainsKey(DesiredSensorRole.GpuCoreTemp),
                _bindings.ContainsKey(DesiredSensorRole.CpuFan),
                _bindings.ContainsKey(DesiredSensorRole.GpuFan));
        }
    }

    private void TryBindFromHardware(IHardware hardware)
    {
        foreach (var sensor in hardware.Sensors)
            TryBindSensor(sensor, hardware);
        foreach (var sub in hardware.SubHardware)
            TryBindFromHardware(sub);
    }

    private void TryBindSensor(ISensor sensor, IHardware hardware)
    {
        var name = sensor.Name?.ToLowerInvariant() ?? "";
        var hwName = hardware.Name?.ToLowerInvariant() ?? "";

        var hwIsCpu = hardware.HardwareType == HardwareType.Cpu;
        var hwIsGpu = hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuIntel;
        var hwIsMobo = hardware.HardwareType == HardwareType.Motherboard;
        var hwIsController = hardware.HardwareType == HardwareType.SuperIO || hardware.HardwareType == HardwareType.EmbeddedController;

        if (sensor.SensorType == SensorType.Temperature)
        {
            if (hwIsCpu || hwName.Contains("cpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (name.Contains("package", StringComparison.OrdinalIgnoreCase)) score += 100;
                if (name.Contains("tdie", StringComparison.OrdinalIgnoreCase) || name.Contains("die", StringComparison.OrdinalIgnoreCase)) score += 80;
                if (name.Contains("ccd", StringComparison.OrdinalIgnoreCase)) score += 70;
                if (name.Contains("core max", StringComparison.OrdinalIgnoreCase) || name.Contains("core (max)", StringComparison.OrdinalIgnoreCase)) score += 60;
                if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) score += 40;
                if (name.Contains("cpu", StringComparison.OrdinalIgnoreCase)) score += 10;
                if (score > 0)
                    Consider(DesiredSensorRole.CpuPackageTemp, sensor, hardware, score);
            }

            if (hwIsGpu || hwName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (name.Contains("core", StringComparison.OrdinalIgnoreCase) || name.Contains("gpu", StringComparison.OrdinalIgnoreCase) || name.Contains("graphics", StringComparison.OrdinalIgnoreCase)) score += 80;
                if (name.Contains("hot spot", StringComparison.OrdinalIgnoreCase) || name.Contains("hotspot", StringComparison.OrdinalIgnoreCase) || name.Contains("junction", StringComparison.OrdinalIgnoreCase)) score += 50;
                if (name.Contains("memory", StringComparison.OrdinalIgnoreCase)) score -= 10;
                if (score > 0)
                    Consider(DesiredSensorRole.GpuCoreTemp, sensor, hardware, score);
            }
        }

        if (sensor.SensorType == SensorType.Clock)
        {
            if (hwIsGpu || hwName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (name.Contains("core", StringComparison.OrdinalIgnoreCase) || name.Contains("gpu", StringComparison.OrdinalIgnoreCase) || name.Contains("graphics", StringComparison.OrdinalIgnoreCase)) score += 80;
                if (name.Contains("memory", StringComparison.OrdinalIgnoreCase) || name.Contains("mem", StringComparison.OrdinalIgnoreCase) || name.Contains("vram", StringComparison.OrdinalIgnoreCase)) score -= 40;
                if (score > 0)
                    Consider(DesiredSensorRole.GpuCoreClockMHz, sensor, hardware, score);

                score = 0;
                if (name.Contains("memory", StringComparison.OrdinalIgnoreCase) || name.Contains("mem", StringComparison.OrdinalIgnoreCase) || name.Contains("vram", StringComparison.OrdinalIgnoreCase)) score += 90;
                if (name.Contains("core", StringComparison.OrdinalIgnoreCase) || name.Contains("gpu", StringComparison.OrdinalIgnoreCase) || name.Contains("graphics", StringComparison.OrdinalIgnoreCase)) score -= 20;
                if (score > 0)
                    Consider(DesiredSensorRole.GpuMemoryClockMHz, sensor, hardware, score);
            }
        }
        if (sensor.SensorType == SensorType.Fan)
        {
            if (hwIsGpu || name.Contains("gpu", StringComparison.OrdinalIgnoreCase) || hwName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 50;
                if (name.Contains("fan", StringComparison.OrdinalIgnoreCase)) score += 10;
                Consider(DesiredSensorRole.GpuFan, sensor, hardware, score);
            }

            var isCpuFanName = name.Contains("cpu", StringComparison.OrdinalIgnoreCase) || name.Contains("cpu fan", StringComparison.OrdinalIgnoreCase);
            var isLikelyCpuFanHardware = hwIsCpu || hwIsMobo || hwIsController;
            if (isLikelyCpuFanHardware)
            {
                var score = 0;
                if (isCpuFanName) score += 100;
                if (name.Contains("pump", StringComparison.OrdinalIgnoreCase)) score -= 30;
                if (name.Contains("aio", StringComparison.OrdinalIgnoreCase)) score -= 10;
                if (name.Contains("fan", StringComparison.OrdinalIgnoreCase)) score += 10;
                if (hwIsController) score += 20;
                if (hwIsCpu) score += 10;
                if (score > 0)
                    Consider(DesiredSensorRole.CpuFan, sensor, hardware, score);
            }
        }

        if (sensor.SensorType == SensorType.Power)
        {
            if (hwIsCpu || hwName.Contains("cpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (name.Contains("package", StringComparison.OrdinalIgnoreCase)) score += 100;
                if (name.Contains("cpu", StringComparison.OrdinalIgnoreCase)) score += 50;
                if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) score += 30;
                if (name.Contains("dram", StringComparison.OrdinalIgnoreCase) || name.Contains("memory", StringComparison.OrdinalIgnoreCase)) score -= 40;
                if (score > 0)
                    Consider(DesiredSensorRole.CpuPowerWatts, sensor, hardware, score);
            }

            if (hwIsGpu || hwName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (name.Contains("board", StringComparison.OrdinalIgnoreCase) || name.Contains("total", StringComparison.OrdinalIgnoreCase)) score += 80;
                if (name.Contains("gpu", StringComparison.OrdinalIgnoreCase) || name.Contains("graphics", StringComparison.OrdinalIgnoreCase)) score += 60;
                if (name.Contains("power", StringComparison.OrdinalIgnoreCase)) score += 40;
                if (score > 0)
                    Consider(DesiredSensorRole.GpuPowerWatts, sensor, hardware, score);
            }
        }

        if (sensor.SensorType == SensorType.Load)
        {
            if (hwIsGpu || hwName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (name.Contains("core", StringComparison.OrdinalIgnoreCase) || name.Contains("gpu", StringComparison.OrdinalIgnoreCase) || name.Contains("graphics", StringComparison.OrdinalIgnoreCase)) score += 90;
                if (name.Contains("memory", StringComparison.OrdinalIgnoreCase) || name.Contains("mem", StringComparison.OrdinalIgnoreCase)) score -= 40;
                if (name.Contains("video", StringComparison.OrdinalIgnoreCase)) score -= 20;
                if (name.Contains("bus", StringComparison.OrdinalIgnoreCase)) score -= 20;
                if (score > 0)
                    Consider(DesiredSensorRole.GpuUsagePercent, sensor, hardware, score);
            }
        }

        if (sensor.SensorType == SensorType.SmallData)
        {
            if (hwIsGpu || hwName.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            {
                {
                    var score = 0;
                    if (name.Contains("used", StringComparison.OrdinalIgnoreCase)) score += 90;
                    if (name.Contains("memory", StringComparison.OrdinalIgnoreCase) || name.Contains("gpu", StringComparison.OrdinalIgnoreCase)) score += 50;
                    if (name.Contains("total", StringComparison.OrdinalIgnoreCase)) score -= 80;
                    if (name.Contains("free", StringComparison.OrdinalIgnoreCase)) score -= 80;
                    if (score > 0)
                        Consider(DesiredSensorRole.GpuVramUsedMb, sensor, hardware, score);
                }

                {
                    var score = 0;
                    if (name.Contains("total", StringComparison.OrdinalIgnoreCase)) score += 90;
                    if (name.Contains("memory", StringComparison.OrdinalIgnoreCase) || name.Contains("gpu", StringComparison.OrdinalIgnoreCase)) score += 50;
                    if (name.Contains("used", StringComparison.OrdinalIgnoreCase)) score -= 80;
                    if (name.Contains("free", StringComparison.OrdinalIgnoreCase)) score -= 80;
                    if (score > 0)
                        Consider(DesiredSensorRole.GpuVramTotalMb, sensor, hardware, score);
                }
            }
        }

        if (sensor.SensorType == SensorType.Clock)
        {
            if (hwIsCpu || hwName.Contains("cpu", StringComparison.OrdinalIgnoreCase))
            {
                var score = 0;
                if (name.Contains("effective", StringComparison.OrdinalIgnoreCase)) score += 100;
                if (name.Contains("average", StringComparison.OrdinalIgnoreCase)) score += 80;
                if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) score += 40;
                if (name.Contains("bus", StringComparison.OrdinalIgnoreCase)) score -= 50;
                if (name.Contains("ring", StringComparison.OrdinalIgnoreCase)) score -= 30;
                if (name.Contains("uncore", StringComparison.OrdinalIgnoreCase)) score -= 30;
                if (score > 0)
                    Consider(DesiredSensorRole.CpuEffectiveClockMHz, sensor, hardware, score);
            }
        }
    }

    private void Consider(DesiredSensorRole role, ISensor sensor, IHardware hardware, int score)
    {
        if (_candidates.TryGetValue(role, out var existing))
        {
            if (score <= existing.Score)
                return;
        }
        _candidates[role] = (sensor, hardware, score);
        _logger.LogDebug("Candidate for {Role}: {HardwareType} {Hardware} - {Sensor} (score {Score})",
            role, hardware.HardwareType, hardware.Name, sensor.Name, score);
    }

    public IReadOnlyDictionary<DesiredSensorRole, BoundSensor?> GetBindingSummary()
    {
        lock (_bindingsLock)
        {
            var result = new Dictionary<DesiredSensorRole, BoundSensor?>();
            foreach (DesiredSensorRole role in Enum.GetValues(typeof(DesiredSensorRole)))
            {
                result[role] = _boundInfo.TryGetValue(role, out var info) ? info : null;
            }
            return result;
        }
    }

    /// <summary>
    /// Read current value for the role. Call only on the worker thread.
    /// </summary>
    public float? GetValue(DesiredSensorRole role)
    {
        lock (_bindingsLock)
        {
            if (_bindings.TryGetValue(role, out var sensor) && sensor != null)
            {
                // Return null if sensor value is null (sensor may be temporarily unavailable)
                return sensor.Value;
            }
            return null;
        }
    }
}
