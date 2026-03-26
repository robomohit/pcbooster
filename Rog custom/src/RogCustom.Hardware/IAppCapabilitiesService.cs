namespace RogCustom.Hardware;

/// <summary>
/// Exposes capability flags and last error for the Capabilities view.
/// Power plan, GPU, and Fan control availability; admin status; last error message.
/// </summary>
public interface IAppCapabilitiesService
{
    bool IsAdmin { get; }
    bool PowerPlanControlAvailable { get; }
    bool MonitorAvailable { get; }
    bool NvidiaGpuControlAvailable { get; }
    bool AmdGpuControlAvailable { get; }
    bool FanControlBridgeConnected { get; }
    string? LastError { get; }

    void SetPowerPlanControlAvailable(bool value);
    void SetMonitorAvailable(bool value);
    void SetNvidiaGpuControlAvailable(bool value);
    void SetFanControlBridgeConnected(bool value);
    void SetLastError(string? message);
    void ClearLastError();
}
