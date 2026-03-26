using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using RogCustom.Hardware;

namespace RogCustom.App.ViewModels;

public sealed class DiagnosticsViewModel : INotifyPropertyChanged
{
    private readonly IHardwareMonitor _monitor;

    private string? _lastUpdated;
    private bool _isLimitedMode;
    private string? _lastError;
    private bool _refreshPending;

    public DiagnosticsViewModel(IHardwareMonitor monitor)
    {
        _monitor = monitor;
        ScheduleRefresh();
    }

    public string? LastUpdated
    {
        get => _lastUpdated;
        private set { if (_lastUpdated != value) { _lastUpdated = value; OnPropertyChanged(); } }
    }

    public bool IsLimitedMode
    {
        get => _isLimitedMode;
        private set { if (_isLimitedMode != value) { _isLimitedMode = value; OnPropertyChanged(); } }
    }

    public string? LastError
    {
        get => _lastError;
        private set { if (_lastError != value) { _lastError = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<BindingRow> Bindings { get; } = new();

    public ObservableCollection<SensorRow> Sensors { get; } = new();

    public void Refresh()
    {
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (_refreshPending) return;
        _refreshPending = true;
        _monitor.RequestDiagnosticsRefresh();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _refreshPending = false;
            LoadSnapshot();
        };
        timer.Start();
    }

    private void LoadSnapshot()
    {
        var snap = _monitor.GetDiagnosticsSnapshot();

        LastUpdated = snap.Timestamp == DateTimeOffset.MinValue ? "(not yet)" : snap.Timestamp.LocalDateTime.ToString("G");
        IsLimitedMode = snap.IsLimitedMode;
        LastError = snap.LastError;

        Bindings.Clear();
        foreach (var kvp in snap.Bindings)
        {
            var role = kvp.Key.ToString();
            var bound = kvp.Value;
            if (bound == null)
                Bindings.Add(new BindingRow(role, "(unbound)", "", "", "Unbound"));
            else
                Bindings.Add(new BindingRow(role, bound.HardwareName, bound.SensorType, bound.SensorName, bound.Status.ToString()));
        }

        Sensors.Clear();
        foreach (var hw in snap.Hardware)
            FlattenHardware(hw);

        void FlattenHardware(DiagnosticsHardware h)
        {
            foreach (var s in h.Sensors)
                Sensors.Add(new SensorRow(h.HardwareType, h.HardwareName, s.SensorType, s.SensorName, s.Value));
            foreach (var sub in h.SubHardware)
                FlattenHardware(sub);
        }
    }

    public void Rebind()
    {
        _monitor.RequestRebind();
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record BindingRow(string Role, string Hardware, string SensorType, string SensorName, string Status);

public sealed record SensorRow(string HardwareType, string Hardware, string SensorType, string SensorName, float? Value);
