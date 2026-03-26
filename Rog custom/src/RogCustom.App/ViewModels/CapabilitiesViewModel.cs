using System.ComponentModel;
using System.Runtime.CompilerServices;
using RogCustom.Hardware;

namespace RogCustom.App.ViewModels;

public sealed class CapabilitiesViewModel : INotifyPropertyChanged
{
    private readonly IAppCapabilitiesService _capabilities;
    private readonly IHardwareMonitor _monitor;

    public CapabilitiesViewModel(IAppCapabilitiesService capabilities, IHardwareMonitor monitor)
    {
        _capabilities = capabilities;
        _monitor = monitor;
    }

    public bool IsAdmin => _capabilities.IsAdmin;
    public bool PowerPlanControlAvailable => _capabilities.PowerPlanControlAvailable;
    public bool NvidiaGpuControlAvailable => _capabilities.NvidiaGpuControlAvailable;
    public bool AmdGpuControlAvailable => _capabilities.AmdGpuControlAvailable;
    public bool FanControlBridgeConnected => _capabilities.FanControlBridgeConnected;
    public string? LastError => _capabilities.LastError;
    public bool IsLimitedMode => _monitor.IsLimitedMode;

    public void Refresh()
    {
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(PowerPlanControlAvailable));
        OnPropertyChanged(nameof(NvidiaGpuControlAvailable));
        OnPropertyChanged(nameof(AmdGpuControlAvailable));
        OnPropertyChanged(nameof(FanControlBridgeConnected));
        OnPropertyChanged(nameof(LastError));
        OnPropertyChanged(nameof(IsLimitedMode));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
