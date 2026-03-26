using System.Security.Principal;



namespace RogCustom.Hardware;

/// <summary>
/// Implementation of IAppCapabilitiesService that tracks what hardware features are available.
/// </summary>
public sealed class AppCapabilitiesService : IAppCapabilitiesService
{
    private bool _powerPlanControlAvailable = true;
    private string? _lastError;

    public bool IsAdmin
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public bool PowerPlanControlAvailable => _powerPlanControlAvailable;
    public bool MonitorAvailable { get; private set; }
    public bool NvidiaGpuControlAvailable { get; private set; }
    public bool AmdGpuControlAvailable => false;
    public bool FanControlBridgeConnected { get; private set; }
    public string? LastError => _lastError;

    public void SetPowerPlanControlAvailable(bool value) => _powerPlanControlAvailable = value;
    public void SetMonitorAvailable(bool value) => MonitorAvailable = value;
    public void SetNvidiaGpuControlAvailable(bool value) => NvidiaGpuControlAvailable = value;
    public void SetFanControlBridgeConnected(bool value) => FanControlBridgeConnected = value;
    public void SetLastError(string? message) => _lastError = message;
    public void ClearLastError() => _lastError = null;
}
