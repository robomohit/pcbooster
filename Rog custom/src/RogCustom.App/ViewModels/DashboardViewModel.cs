using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using RogCustom.Core;
using RogCustom.Hardware;

namespace RogCustom.App.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IHardwareMonitor _monitor;
    private readonly IModeOrchestrator _modeOrchestrator;
    private readonly IProfileStore _profileStore;
    private readonly IAppCapabilitiesService _capabilities;
    private readonly DispatcherTimer _timer;
    private string? _lastError;
    private string? _cpuPackageTemp;
    private string? _gpuCoreTemp;
    private string? _cpuFanRpm;
    private string? _gpuFanRpm;
    private string? _cpuUsage;
    private string? _ramUsage;
    private string? _cpuPower;
    private string? _gpuPower;
    private string? _gpuUsage;
    private string? _gpuVram;
    private string? _cpuClock;
    private string? _gpuCoreClock;
    private string? _gpuMemClock;
    private string? _gpuName;
    private bool _isLimitedMode;
    private string _currentModeName = "";
    private bool _disposed;

    private double _cpuTempValue;
    private double _gpuTempValue;
    private double _cpuUsageValue;
    private double _gpuUsageValue;
    private double _ramUsagePercent;
    private double _gpuVramPercent;
    private double _cpuPowerValue;
    private double _gpuPowerValue;

    public DashboardViewModel(
        IHardwareMonitor monitor,
        IModeOrchestrator modeOrchestrator,
        IProfileStore profileStore,
        IAppCapabilitiesService capabilities)
    {
        _monitor = monitor;
        _modeOrchestrator = modeOrchestrator;
        _profileStore = profileStore;
        _capabilities = capabilities;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
        _currentModeName = _profileStore.Load().LastActiveMode.ToString();
        RefreshSnapshot();
    }

    public string? LastError
    {
        get => _lastError;
        private set { if (_lastError != value) { _lastError = value; OnPropertyChanged(); } }
    }

    public string? CpuPackageTemp
    {
        get => _cpuPackageTemp;
        private set { if (_cpuPackageTemp != value) { _cpuPackageTemp = value; OnPropertyChanged(); } }
    }

    public string? GpuCoreTemp
    {
        get => _gpuCoreTemp;
        private set { if (_gpuCoreTemp != value) { _gpuCoreTemp = value; OnPropertyChanged(); } }
    }

    public string? CpuFanRpm
    {
        get => _cpuFanRpm;
        private set { if (_cpuFanRpm != value) { _cpuFanRpm = value; OnPropertyChanged(); } }
    }

    public string? GpuFanRpm
    {
        get => _gpuFanRpm;
        private set { if (_gpuFanRpm != value) { _gpuFanRpm = value; OnPropertyChanged(); } }
    }

    public string? CpuUsage
    {
        get => _cpuUsage;
        private set { if (_cpuUsage != value) { _cpuUsage = value; OnPropertyChanged(); } }
    }

    public string? RamUsage
    {
        get => _ramUsage;
        private set { if (_ramUsage != value) { _ramUsage = value; OnPropertyChanged(); } }
    }

    public string? CpuPower
    {
        get => _cpuPower;
        private set { if (_cpuPower != value) { _cpuPower = value; OnPropertyChanged(); } }
    }

    public string? GpuPower
    {
        get => _gpuPower;
        private set { if (_gpuPower != value) { _gpuPower = value; OnPropertyChanged(); } }
    }

    public string? GpuUsage
    {
        get => _gpuUsage;
        private set { if (_gpuUsage != value) { _gpuUsage = value; OnPropertyChanged(); } }
    }

    public string? GpuVram
    {
        get => _gpuVram;
        private set { if (_gpuVram != value) { _gpuVram = value; OnPropertyChanged(); } }
    }

    public string? CpuClock
    {
        get => _cpuClock;
        private set { if (_cpuClock != value) { _cpuClock = value; OnPropertyChanged(); } }
    }

    public string? GpuCoreClock
    {
        get => _gpuCoreClock;
        private set { if (_gpuCoreClock != value) { _gpuCoreClock = value; OnPropertyChanged(); } }
    }

    public string? GpuMemClock
    {
        get => _gpuMemClock;
        private set { if (_gpuMemClock != value) { _gpuMemClock = value; OnPropertyChanged(); } }
    }

    public string? GpuName
    {
        get => _gpuName;
        private set { if (_gpuName != value) { _gpuName = value; OnPropertyChanged(); } }
    }

    public string CurrentModeName
    {
        get => _currentModeName;
        private set
        {
            if (_currentModeName != value)
            {
                _currentModeName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSilentActive));
                OnPropertyChanged(nameof(IsWindowsActive));
                OnPropertyChanged(nameof(IsBalancedActive));
                OnPropertyChanged(nameof(IsPerformanceActive));
                OnPropertyChanged(nameof(IsTurboActive));
                OnPropertyChanged(nameof(IsManualActive));
            }
        }
    }

    public bool IsSilentActive => _currentModeName == "Silent";
    public bool IsWindowsActive => _currentModeName == "Windows";
    public bool IsBalancedActive => _currentModeName == "Balanced";
    public bool IsPerformanceActive => _currentModeName == "Performance";
    public bool IsTurboActive => _currentModeName == "Turbo";
    public bool IsManualActive => _currentModeName == "Manual";

    public bool IsLimitedMode
    {
        get => _isLimitedMode;
        private set { if (_isLimitedMode != value) { _isLimitedMode = value; OnPropertyChanged(); } }
    }

    public double CpuTempValue
    {
        get => _cpuTempValue;
        private set { if (Math.Abs(_cpuTempValue - value) > 0.01) { _cpuTempValue = value; OnPropertyChanged(); } }
    }

    public double GpuTempValue
    {
        get => _gpuTempValue;
        private set { if (Math.Abs(_gpuTempValue - value) > 0.01) { _gpuTempValue = value; OnPropertyChanged(); } }
    }

    public double CpuUsageValue
    {
        get => _cpuUsageValue;
        private set { if (Math.Abs(_cpuUsageValue - value) > 0.01) { _cpuUsageValue = value; OnPropertyChanged(); } }
    }

    public double GpuUsageValue
    {
        get => _gpuUsageValue;
        private set { if (Math.Abs(_gpuUsageValue - value) > 0.01) { _gpuUsageValue = value; OnPropertyChanged(); } }
    }

    public double RamUsagePercent
    {
        get => _ramUsagePercent;
        private set { if (Math.Abs(_ramUsagePercent - value) > 0.01) { _ramUsagePercent = value; OnPropertyChanged(); } }
    }

    public double GpuVramPercent
    {
        get => _gpuVramPercent;
        private set { if (Math.Abs(_gpuVramPercent - value) > 0.01) { _gpuVramPercent = value; OnPropertyChanged(); } }
    }

    public double CpuPowerValue
    {
        get => _cpuPowerValue;
        private set { if (Math.Abs(_cpuPowerValue - value) > 0.01) { _cpuPowerValue = value; OnPropertyChanged(); } }
    }

    public double GpuPowerValue
    {
        get => _gpuPowerValue;
        private set { if (Math.Abs(_gpuPowerValue - value) > 0.01) { _gpuPowerValue = value; OnPropertyChanged(); } }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_disposed) return;
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        var snapshot = _monitor.GetLastSnapshot();

        CpuPackageTemp = snapshot.CpuPackageTemp.HasValue ? $"{snapshot.CpuPackageTemp.Value:F0}°C" : "—";
        GpuCoreTemp = snapshot.GpuCoreTemp.HasValue ? $"{snapshot.GpuCoreTemp.Value:F0}°C" : "—";
        CpuFanRpm = snapshot.CpuFanRpm.HasValue ? $"{snapshot.CpuFanRpm.Value:F0} RPM" : "—";
        GpuFanRpm = snapshot.GpuFanRpm.HasValue ? $"{snapshot.GpuFanRpm.Value:F0} RPM" : "—";
        CpuUsage = snapshot.CpuUsagePercent.HasValue ? $"{snapshot.CpuUsagePercent.Value:F0}%" : "—";
        RamUsage = snapshot.RamUsedMb.HasValue && snapshot.RamTotalMb.HasValue
            ? $"{snapshot.RamUsedMb.Value / 1024f:F1} / {snapshot.RamTotalMb.Value / 1024f:F1} GB"
            : "—";
        CpuPower = snapshot.CpuPowerWatts.HasValue ? $"{snapshot.CpuPowerWatts.Value:F1}W" : "—";
        GpuPower = snapshot.GpuPowerWatts.HasValue ? $"{snapshot.GpuPowerWatts.Value:F1}W" : "—";
        GpuUsage = snapshot.GpuUsagePercent.HasValue ? $"{snapshot.GpuUsagePercent.Value:F0}%" : "—";
        GpuVram = snapshot.GpuVramUsedMb.HasValue && snapshot.GpuVramTotalMb.HasValue
            ? $"{snapshot.GpuVramUsedMb.Value / 1024f:F1} / {snapshot.GpuVramTotalMb.Value / 1024f:F1} GB"
            : snapshot.GpuVramUsedMb.HasValue ? $"{snapshot.GpuVramUsedMb.Value:F0} MB" : "—";
        CpuClock = snapshot.CpuEffectiveClockMHz.HasValue ? $"{snapshot.CpuEffectiveClockMHz.Value:F0} MHz" : "—";
        GpuCoreClock = snapshot.GpuCoreClockMHz.HasValue ? $"{snapshot.GpuCoreClockMHz.Value:F0} MHz" : "—";
        GpuMemClock = snapshot.GpuMemoryClockMHz.HasValue ? $"{snapshot.GpuMemoryClockMHz.Value:F0} MHz" : "—";
        GpuName = snapshot.GpuName ?? "GPU";

        CpuTempValue = snapshot.CpuPackageTemp.HasValue ? Math.Clamp(snapshot.CpuPackageTemp.Value, 0, 100) : 0;
        GpuTempValue = snapshot.GpuCoreTemp.HasValue ? Math.Clamp(snapshot.GpuCoreTemp.Value, 0, 100) : 0;
        CpuUsageValue = snapshot.CpuUsagePercent ?? 0;
        GpuUsageValue = snapshot.GpuUsagePercent ?? 0;
        RamUsagePercent = (snapshot.RamUsedMb.HasValue && snapshot.RamTotalMb.HasValue && snapshot.RamTotalMb.Value > 0)
            ? Math.Clamp(snapshot.RamUsedMb.Value / snapshot.RamTotalMb.Value * 100, 0, 100) : 0;
        GpuVramPercent = (snapshot.GpuVramUsedMb.HasValue && snapshot.GpuVramTotalMb.HasValue && snapshot.GpuVramTotalMb.Value > 0)
            ? Math.Clamp(snapshot.GpuVramUsedMb.Value / snapshot.GpuVramTotalMb.Value * 100, 0, 100) : 0;
        CpuPowerValue = snapshot.CpuPowerWatts.HasValue ? Math.Clamp(snapshot.CpuPowerWatts.Value, 0, 200) : 0;
        GpuPowerValue = snapshot.GpuPowerWatts.HasValue ? Math.Clamp(snapshot.GpuPowerWatts.Value, 0, 300) : 0;

        LastError = _capabilities.LastError;
        IsLimitedMode = _monitor.IsLimitedMode;
    }

    public void SetMode(PerformanceMode mode)
    {
        if (!_modeOrchestrator.ApplyMode(mode))
        {
            LastError = _capabilities.LastError;
            MainWindow.Instance?.ShowToast($"Failed to apply {mode} mode");
            return;
        }
        CurrentModeName = mode.ToString();
        LastError = null;
        MainWindow.Instance?.ShowToast($"{mode} mode applied");
    }

    public void RefreshSensors()
    {
        _monitor.RequestRebind();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
