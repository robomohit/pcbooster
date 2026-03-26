namespace RogCustom.Hardware;

public sealed class StubGpuControlService : IGpuControlService
{
    public bool IsSupported => false;
    public bool IsConnected => false;
    public string? GpuName => null;
    public float? DefaultPowerLimitWatts => null;
    public float? MinPowerLimitWatts => null;
    public float? MaxPowerLimitWatts => null;
    public float? CurrentPowerLimitWatts => null;
    public bool SetPowerLimit(float watts) => false;
    public bool RestoreDefaultPowerLimit() => false;

    // OC stubs
    public int? MaxSupportedGpuClockMHz => null;
    public int? MaxSupportedMemClockMHz => null;
    public int? CurrentGpuClockMHz => null;
    public int? CurrentMemClockMHz => null;
    public bool LockGpuClocks(int maxMHz) => false;
    public bool LockMemoryClocks(int maxMHz) => false;
    public bool ResetGpuClocks() => false;
    public bool ResetMemoryClocks() => false;
    public string? QueryCurrentClocks() => null;

    // OC Scanner
    public bool IsOcScanning => false;
    public int OcScanProgressPercent => 0;
    public string? OcScanStatusMessage => null;
    public int? OcScanCurrentTestMHz => null;
    public void StartOcScan() { }
    public void CancelOcScan() { }
}
