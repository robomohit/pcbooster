namespace RogCustom.Hardware;

public interface IGpuControlService
{
    bool IsSupported { get; }
    bool IsConnected { get; }
    string? GpuName { get; }
    float? DefaultPowerLimitWatts { get; }
    float? MinPowerLimitWatts { get; }
    float? MaxPowerLimitWatts { get; }
    float? CurrentPowerLimitWatts { get; }
    bool SetPowerLimit(float watts);
    bool RestoreDefaultPowerLimit();

    // OC: Clock locking
    int? MaxSupportedGpuClockMHz { get; }
    int? MaxSupportedMemClockMHz { get; }
    int? CurrentGpuClockMHz { get; }
    int? CurrentMemClockMHz { get; }
    bool LockGpuClocks(int maxMHz);
    bool LockMemoryClocks(int maxMHz);
    bool ResetGpuClocks();
    bool ResetMemoryClocks();
    string? QueryCurrentClocks(); // returns JSON-style info

    // OC Scanner
    bool IsOcScanning { get; }
    int OcScanProgressPercent { get; }
    string? OcScanStatusMessage { get; }
    int? OcScanCurrentTestMHz { get; }
    void StartOcScan();
    void CancelOcScan();
}
