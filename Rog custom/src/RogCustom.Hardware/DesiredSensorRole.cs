namespace RogCustom.Hardware;

/// <summary>
/// Logical roles for sensors we want to expose. SensorBindingLayer maps these to LHM sensors.
/// </summary>
public enum DesiredSensorRole
{
    CpuPackageTemp,
    GpuCoreTemp,
    GpuCoreClockMHz,
    GpuMemoryClockMHz,
    CpuFan,
    GpuFan,
    CpuPowerWatts,
    GpuPowerWatts,
    GpuUsagePercent,
    GpuVramUsedMb,
    GpuVramTotalMb,
    CpuEffectiveClockMHz,
}
