using System.ComponentModel;
using System.Runtime.CompilerServices;
using RogCustom.Hardware;

namespace RogCustom.App.ViewModels;

public sealed class FansViewModel : INotifyPropertyChanged
{
    private readonly IFanBridgeService _fanBridge;
    private readonly IAppCapabilitiesService _capabilities;
    private string? _lastError;
    private bool _fanControlDetected;
    private bool _fanControlRunning;
    private string? _currentProfile;

    public FansViewModel(IFanBridgeService fanBridge, IAppCapabilitiesService capabilities)
    {
        _fanBridge = fanBridge;
        _capabilities = capabilities;
        Refresh();
    }

    public string? LastError
    {
        get => _lastError;
        private set { if (_lastError != value) { _lastError = value; OnPropertyChanged(); } }
    }

    public bool FanControlDetected
    {
        get => _fanControlDetected;
        private set { if (_fanControlDetected != value) { _fanControlDetected = value; OnPropertyChanged(); } }
    }

    public bool FanControlRunning
    {
        get => _fanControlRunning;
        private set { if (_fanControlRunning != value) { _fanControlRunning = value; OnPropertyChanged(); } }
    }

    public string? CurrentProfile
    {
        get => _currentProfile;
        private set { if (_currentProfile != value) { _currentProfile = value; OnPropertyChanged(); } }
    }

    public void Refresh()
    {
        FanControlDetected = _fanBridge.IsSupported;
        FanControlRunning = _fanBridge.IsConnected;
        CurrentProfile = _fanBridge.CurrentProfileId ?? "(none)";
        LastError = _capabilities.LastError;
    }

    public void ApplyProfile(string profileId)
    {
        if (_fanBridge.ApplyProfile(profileId))
        {
            CurrentProfile = profileId;
            LastError = null;
        }
        else
        {
            LastError = $"Failed to apply fan profile: {profileId}";
        }
        Refresh();
    }

    public void RestoreDefaults()
    {
        if (_fanBridge.RestoreDefaults())
            LastError = null;
        else
            LastError = "Failed to restore fan defaults";
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
