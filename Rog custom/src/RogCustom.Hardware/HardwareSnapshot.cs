namespace RogCustom.Hardware;

/// <summary>
/// Immutable DTO for sensor values. Only primitive/nullable types; no LHM types.
/// Used for thread-safe handoff from worker to UI.
/// </summary>
public sealed record HardwareSnapshot(
    float? CpuPackageTemp,
    float? GpuCoreTemp,
    float? CpuFanRpm,
    float? GpuFanRpm,
    string? GpuName,
    float? GpuCoreClockMHz,
    float? GpuMemoryClockMHz,
    float? CpuUsagePercent,
    float? RamUsedMb,
    float? RamTotalMb,
    float? CpuPowerWatts,
    float? GpuPowerWatts,
    float? GpuUsagePercent,
    float? GpuVramUsedMb,
    float? GpuVramTotalMb,
    float? CpuEffectiveClockMHz,
    DateTimeOffset Timestamp)
{
    public static HardwareSnapshot Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, DateTimeOffset.MinValue);
}
