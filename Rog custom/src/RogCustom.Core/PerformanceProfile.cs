using System.Text.Json.Serialization;

namespace RogCustom.Core;

public enum CpuBoostPolicy
{
    Disabled = 0,
    EfficientEnabled = 1,
    Enabled = 2,
    EfficientAggressive = 3,
    Aggressive = 4,
}

public sealed class ModeSettings
{
    [JsonPropertyName("powerPlanGuid")]
    public string? PowerPlanGuid { get; set; }

    [JsonPropertyName("cpuBoost")]
    public CpuBoostPolicy CpuBoost { get; set; } = CpuBoostPolicy.Enabled;

    [JsonPropertyName("fanCurveId")]
    public string? FanCurveId { get; set; }

    [JsonPropertyName("coreParking")]
    public bool CoreParking { get; set; } = false;

    [JsonPropertyName("maxProcessorStatePercent")]
    public int MaxProcessorStatePercent { get; set; } = 100;

    [JsonPropertyName("gpuPowerLimitWatts")]
    public float? GpuPowerLimitWatts { get; set; }
}

public sealed class NamedProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Default";

    [JsonPropertyName("modes")]
    public Dictionary<PerformanceMode, ModeSettings> Modes { get; set; } = new();
}

public sealed class PerformanceProfile
{
    public const int CurrentSchemaVersion = 3;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("lastActiveMode")]
    public PerformanceMode LastActiveMode { get; set; } = PerformanceMode.Balanced;

    [JsonPropertyName("activeProfileName")]
    public string ActiveProfileName { get; set; } = "Default";

    [JsonPropertyName("profiles")]
    public List<NamedProfile> Profiles { get; set; } = new();

    [JsonPropertyName("modeToGuid")]
    public Dictionary<PerformanceMode, string> ModeToGuid { get; set; } = new();

    public NamedProfile GetActiveProfile()
    {
        var profile = Profiles.FirstOrDefault(p =>
            string.Equals(p.Name, ActiveProfileName, StringComparison.OrdinalIgnoreCase));
        if (profile != null) return profile;

        profile = new NamedProfile { Name = ActiveProfileName };
        Profiles.Add(profile);
        return profile;
    }

    public ModeSettings GetModeSettings(PerformanceMode mode)
    {
        var active = GetActiveProfile();
        if (active.Modes.TryGetValue(mode, out var settings))
            return settings;

        settings = new ModeSettings();
        if (ModeToGuid.TryGetValue(mode, out var guid))
            settings.PowerPlanGuid = guid;
        active.Modes[mode] = settings;
        return settings;
    }
}
