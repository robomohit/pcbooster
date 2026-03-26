using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using RogCustom.Core;
using RogCustom.Hardware;

namespace RogCustom.App.ViewModels;

public sealed class ProfilesViewModel : INotifyPropertyChanged
{
    private readonly IProfileStore _profileStore;
    private readonly IPowerPlanService _powerPlanService;
    private PerformanceProfile _profile = new();
    private string? _lastError;
    private Guid? _silentPlanGuid;
    private Guid? _balancedPlanGuid;
    private Guid? _performancePlanGuid;
    private Guid? _turboPlanGuid;
    private string? _selectedProfileName;
    private string? _newProfileName;

    public ProfilesViewModel(IProfileStore profileStore, IPowerPlanService powerPlanService)
    {
        _profileStore = profileStore;
        _powerPlanService = powerPlanService;
        LoadProfile();
        LoadAvailablePlans();
    }

    public string? LastError
    {
        get => _lastError;
        private set { if (_lastError != value) { _lastError = value; OnPropertyChanged(); } }
    }

    public Guid? SilentPlanGuid
    {
        get => _silentPlanGuid;
        set { if (_silentPlanGuid != value) { _silentPlanGuid = value; OnPropertyChanged(); } }
    }

    public Guid? BalancedPlanGuid
    {
        get => _balancedPlanGuid;
        set { if (_balancedPlanGuid != value) { _balancedPlanGuid = value; OnPropertyChanged(); } }
    }

    public Guid? PerformancePlanGuid
    {
        get => _performancePlanGuid;
        set { if (_performancePlanGuid != value) { _performancePlanGuid = value; OnPropertyChanged(); } }
    }

    public Guid? TurboPlanGuid
    {
        get => _turboPlanGuid;
        set { if (_turboPlanGuid != value) { _turboPlanGuid = value; OnPropertyChanged(); } }
    }

    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            if (_selectedProfileName != value)
            {
                _selectedProfileName = value;
                OnPropertyChanged();
                if (value != null)
                {
                    _profileStore.SetActiveProfile(value);
                    LoadProfile();
                }
            }
        }
    }

    public string? NewProfileName
    {
        get => _newProfileName;
        set { if (_newProfileName != value) { _newProfileName = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<string> ProfileNames { get; } = new();
    public List<PowerPlanOption> AvailablePlans { get; private set; } = new();
    public ObservableCollection<ModeDetailRow> ModeDetails { get; } = new();

    private void LoadProfile()
    {
        _profile = _profileStore.Load();
        SilentPlanGuid = _profileStore.GetGuidForMode(PerformanceMode.Silent);
        BalancedPlanGuid = _profileStore.GetGuidForMode(PerformanceMode.Balanced);
        PerformancePlanGuid = _profileStore.GetGuidForMode(PerformanceMode.Performance);
        TurboPlanGuid = _profileStore.GetGuidForMode(PerformanceMode.Turbo);

        ProfileNames.Clear();
        foreach (var name in _profileStore.GetProfileNames())
            ProfileNames.Add(name);
        _selectedProfileName = _profile.ActiveProfileName;
        OnPropertyChanged(nameof(SelectedProfileName));

        RefreshModeDetails();
    }

    private void RefreshModeDetails()
    {
        ModeDetails.Clear();
        foreach (PerformanceMode mode in Enum.GetValues<PerformanceMode>())
        {
            var settings = _profileStore.GetModeSettings(mode);
            var planName = "(not set)";
            if (!string.IsNullOrWhiteSpace(settings.PowerPlanGuid) && Guid.TryParse(settings.PowerPlanGuid, out _))
            {
                var match = AvailablePlans.FirstOrDefault(p => p.Guid.ToString().Equals(settings.PowerPlanGuid, StringComparison.OrdinalIgnoreCase));
                if (match != null) planName = match.FriendlyName;
            }
            ModeDetails.Add(new ModeDetailRow(
                mode.ToString(),
                planName,
                settings.CpuBoost.ToString(),
                settings.FanCurveId ?? "(default)",
                settings.CoreParking ? "ON" : "OFF"));
        }
    }

    private void LoadAvailablePlans()
    {
        try
        {
            AvailablePlans = _powerPlanService
                .EnumerateSchemes()
                .Select(p => new PowerPlanOption(p.Guid, p.FriendlyName))
                .OrderBy(p => p.FriendlyName)
                .ToList();
            OnPropertyChanged(nameof(AvailablePlans));
        }
        catch (Exception ex)
        {
            LastError = $"Failed to load power plans: {ex.Message}";
        }
    }

    public void SaveProfile()
    {
        try
        {
            _profileStore.SetGuidForMode(PerformanceMode.Silent, SilentPlanGuid ?? Guid.Empty);
            _profileStore.SetGuidForMode(PerformanceMode.Balanced, BalancedPlanGuid ?? Guid.Empty);
            _profileStore.SetGuidForMode(PerformanceMode.Performance, PerformancePlanGuid ?? Guid.Empty);
            _profileStore.SetGuidForMode(PerformanceMode.Turbo, TurboPlanGuid ?? Guid.Empty);
            _profileStore.Save(_profileStore.Load());
            LoadProfile();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to save profile: {ex.Message}";
        }
    }

    public void CreateNewProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName)) return;
        try
        {
            _profileStore.CreateProfile(NewProfileName.Trim());
            _profileStore.SetActiveProfile(NewProfileName.Trim());
            NewProfileName = null;
            LoadProfile();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to create profile: {ex.Message}";
        }
    }

    public void DeleteSelectedProfile()
    {
        if (string.IsNullOrWhiteSpace(SelectedProfileName) || SelectedProfileName == "Default") return;
        try
        {
            _profileStore.DeleteProfile(SelectedProfileName);
            LoadProfile();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to delete profile: {ex.Message}";
        }
    }

    public void SetPlanForMode(PerformanceMode mode, Guid guid)
    {
        try
        {
            _profileStore.SetGuidForMode(mode, guid);
            LoadProfile();
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Failed to set power plan: {ex.Message}";
        }
    }

    public void RestoreDefaults()
    {
        try
        {
            var powerPlans = _powerPlanService.EnumerateSchemes();
            var balanced = powerPlans.FirstOrDefault(p => p.FriendlyName.Contains("Balanced", StringComparison.OrdinalIgnoreCase));
            var powerSaver = powerPlans.FirstOrDefault(p => p.FriendlyName.Contains("Power saver", StringComparison.OrdinalIgnoreCase));
            var highPerf = powerPlans.FirstOrDefault(p => p.FriendlyName.Contains("High performance", StringComparison.OrdinalIgnoreCase) || p.FriendlyName.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase));

            if (balanced.Guid != Guid.Empty)
                _profileStore.SetGuidForMode(PerformanceMode.Balanced, balanced.Guid);
            if (powerSaver.Guid != Guid.Empty)
                _profileStore.SetGuidForMode(PerformanceMode.Silent, powerSaver.Guid);
            if (highPerf.Guid != Guid.Empty)
            {
                _profileStore.SetGuidForMode(PerformanceMode.Performance, highPerf.Guid);
                _profileStore.SetGuidForMode(PerformanceMode.Turbo, highPerf.Guid);
            }
            else if (balanced.Guid != Guid.Empty)
            {
                _profileStore.SetGuidForMode(PerformanceMode.Performance, balanced.Guid);
                _profileStore.SetGuidForMode(PerformanceMode.Turbo, balanced.Guid);
            }

            LoadProfile();
            LastError = "Default power plans restored";
        }
        catch (Exception ex)
        {
            LastError = $"Failed to restore defaults: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record PowerPlanOption(Guid Guid, string FriendlyName);
public sealed record ModeDetailRow(string Mode, string PowerPlan, string CpuBoost, string FanCurve, string CoreParking);
