using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogCustom.Core;

public sealed class ProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _configDirectory;
    private readonly string _profilePath;
    private readonly object _lock = new();
    private PerformanceProfile _current;

    public ProfileStore(string? configDirectory = null)
    {
        _configDirectory = configDirectory ?? ConfigPathHelper.GetConfigDirectory();
        Directory.CreateDirectory(_configDirectory);
        _profilePath = Path.Combine(_configDirectory, "profiles.json");
        _current = LoadFromDisk();
    }

    public PerformanceProfile Load()
    {
        lock (_lock)
        {
            _current = LoadFromDisk();
            return _current;
        }
    }

    public void Save(PerformanceProfile profile)
    {
        lock (_lock)
        {
            _current = profile;
            SaveToDisk(profile);
        }
    }

    public Guid? GetGuidForMode(PerformanceMode mode)
    {
        lock (_lock)
        {
            var settings = _current.GetModeSettings(mode);
            if (!string.IsNullOrWhiteSpace(settings.PowerPlanGuid) && Guid.TryParse(settings.PowerPlanGuid, out var g))
                return g;
            if (_current.ModeToGuid.TryGetValue(mode, out var s) && Guid.TryParse(s, out var g2))
                return g2;
            return null;
        }
    }

    public void SetGuidForMode(PerformanceMode mode, Guid guid)
    {
        lock (_lock)
        {
            var settings = _current.GetModeSettings(mode);
            settings.PowerPlanGuid = guid.ToString();
            _current.ModeToGuid[mode] = guid.ToString();
            SaveToDisk(_current);
        }
    }

    public ModeSettings GetModeSettings(PerformanceMode mode)
    {
        lock (_lock) { return _current.GetModeSettings(mode); }
    }

    public void SetModeSettings(PerformanceMode mode, ModeSettings settings)
    {
        lock (_lock)
        {
            var active = _current.GetActiveProfile();
            active.Modes[mode] = settings;
            if (!string.IsNullOrWhiteSpace(settings.PowerPlanGuid))
                _current.ModeToGuid[mode] = settings.PowerPlanGuid;
            SaveToDisk(_current);
        }
    }

    public List<string> GetProfileNames()
    {
        lock (_lock) { return _current.Profiles.Select(p => p.Name).ToList(); }
    }

    public void SetActiveProfile(string name)
    {
        lock (_lock)
        {
            _current.ActiveProfileName = name;
            SaveToDisk(_current);
        }
    }

    public void CreateProfile(string name)
    {
        lock (_lock)
        {
            if (_current.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                return;
            _current.Profiles.Add(new NamedProfile { Name = name });
            SaveToDisk(_current);
        }
    }

    public void DeleteProfile(string name)
    {
        lock (_lock)
        {
            _current.Profiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(_current.ActiveProfileName, name, StringComparison.OrdinalIgnoreCase))
                _current.ActiveProfileName = _current.Profiles.FirstOrDefault()?.Name ?? "Default";
            SaveToDisk(_current);
        }
    }

    private PerformanceProfile LoadFromDisk()
    {
        if (!File.Exists(_profilePath))
            return CreateDefault();

        try
        {
            var json = File.ReadAllText(_profilePath);
            var profile = JsonSerializer.Deserialize<PerformanceProfile>(json, JsonOptions);
            if (profile == null)
                return CreateDefault();

            if (profile.SchemaVersion < PerformanceProfile.CurrentSchemaVersion)
                Migrate(profile);
            profile.SchemaVersion = PerformanceProfile.CurrentSchemaVersion;
            return profile;
        }
        catch (System.Text.Json.JsonException)
        {
            // Corrupted JSON -- reset to defaults
            return CreateDefault();
        }
        catch (IOException)
        {
            // File I/O failure -- reset to defaults
            return CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            // Permission denied -- reset to defaults
            return CreateDefault();
        }
    }

    private static readonly Dictionary<PerformanceMode, string> WellKnownGuids = new()
    {
        [PerformanceMode.Silent] = "a1841308-3541-4fab-bc81-f71556f20b4a", // Power Saver
        [PerformanceMode.Balanced] = "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
        [PerformanceMode.Performance] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High Performance
        [PerformanceMode.Turbo] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", // High Performance
        [PerformanceMode.Manual] = "381b4222-f694-41f0-9685-ff5bb260df2e", // Balanced
    };

    private static PerformanceProfile CreateDefault()
    {
        var profile = new PerformanceProfile();
        var d = new NamedProfile { Name = "Default" };

        d.Modes[PerformanceMode.Silent] = new ModeSettings
        {
            PowerPlanGuid = WellKnownGuids[PerformanceMode.Silent],
            CpuBoost = CpuBoostPolicy.Disabled,
            MaxProcessorStatePercent = 99, // 99% disables CPU turbo-boost safely!
            CoreParking = false,
            FanCurveId = "quiet",
        };
        d.Modes[PerformanceMode.Windows] = new ModeSettings();
        d.Modes[PerformanceMode.Balanced] = new ModeSettings
        {
            PowerPlanGuid = WellKnownGuids[PerformanceMode.Balanced],
            CpuBoost = CpuBoostPolicy.Enabled,
            MaxProcessorStatePercent = 100,
            CoreParking = false,
            FanCurveId = "normal",
        };
        d.Modes[PerformanceMode.Performance] = new ModeSettings
        {
            PowerPlanGuid = WellKnownGuids[PerformanceMode.Performance],
            CpuBoost = CpuBoostPolicy.Aggressive,
            MaxProcessorStatePercent = 100,
            CoreParking = false,
            FanCurveId = "performance",
        };
        d.Modes[PerformanceMode.Turbo] = new ModeSettings
        {
            PowerPlanGuid = WellKnownGuids[PerformanceMode.Turbo],
            CpuBoost = CpuBoostPolicy.Aggressive,
            MaxProcessorStatePercent = 100,
            CoreParking = false,
            FanCurveId = "max",
        };
        d.Modes[PerformanceMode.Manual] = new ModeSettings
        {
            PowerPlanGuid = WellKnownGuids[PerformanceMode.Manual],
            CpuBoost = CpuBoostPolicy.Enabled,
            MaxProcessorStatePercent = 100,
            CoreParking = false,
            FanCurveId = "normal",
        };

        profile.Profiles.Add(d);

        foreach (var kvp in WellKnownGuids)
            profile.ModeToGuid[kvp.Key] = kvp.Value;

        return profile;
    }

    private static void Migrate(PerformanceProfile profile)
    {
        if (profile.SchemaVersion < 2)
        {
            if (profile.Profiles.Count == 0)
            {
                var named = new NamedProfile { Name = "Default" };
                foreach (var kvp in profile.ModeToGuid)
                {
                    named.Modes[kvp.Key] = new ModeSettings { PowerPlanGuid = kvp.Value };
                }
                profile.Profiles.Add(named);
                profile.ActiveProfileName = "Default";
            }
        }

        if (profile.SchemaVersion < 3)
        {
            var defaults = CreateDefault();
            var defaultModes = defaults.GetActiveProfile().Modes;

            foreach (var namedProfile in profile.Profiles)
            {
                foreach (var mode in Enum.GetValues<PerformanceMode>())
                {
                    if (!namedProfile.Modes.ContainsKey(mode))
                    {
                        namedProfile.Modes[mode] = defaultModes.TryGetValue(mode, out var d)
                            ? d : new ModeSettings();
                    }
                    else
                    {
                        var existing = namedProfile.Modes[mode];
                        if (string.IsNullOrWhiteSpace(existing.PowerPlanGuid) &&
                            WellKnownGuids.TryGetValue(mode, out var guid))
                            existing.PowerPlanGuid = guid;
                    }
                }
            }

            foreach (var kvp in WellKnownGuids)
            {
                if (!profile.ModeToGuid.ContainsKey(kvp.Key))
                    profile.ModeToGuid[kvp.Key] = kvp.Value;
            }
        }
    }

    private void SaveToDisk(PerformanceProfile profile)
    {
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(_profilePath, json);
    }
}
