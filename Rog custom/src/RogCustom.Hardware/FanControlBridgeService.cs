using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RogCustom.Hardware;

public sealed class FanControlBridgeService : IFanBridgeService, IDisposable
{
    private readonly ILogger<FanControlBridgeService> _logger;
    private string? _fanControlPath;
    private string? _fanControlConfigDir;
    private string? _backupConfigPath;
    private bool _disposed;

    private static readonly Regex SafeProfileIdRegex = new(@"^[A-Za-z0-9_\-]+$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> DefaultProfileDescriptions = new()
    {
        ["quiet"] = "RogCustom Silent mode",
        ["normal"] = "RogCustom Balanced mode",
        ["performance"] = "RogCustom Performance mode",
        ["max"] = "RogCustom Turbo mode",
    };

    public FanControlBridgeService(ILogger<FanControlBridgeService> logger)
    {
        _logger = logger;
        DetectInstallation();
        EnsureProfileStubs();
    }

    public bool IsSupported => _fanControlPath != null;
    public bool IsConnected => IsSupported && IsFanControlRunning();
    public string? CurrentProfileId { get; private set; }

    public bool ApplyProfile(string profileId)
    {
        if (!IsSupported || _fanControlConfigDir == null)
        {
            _logger.LogWarning("FanControl not detected, cannot apply profile '{Profile}'", profileId);
            return false;
        }

        if (!SafeProfileIdRegex.IsMatch(profileId))
        {
            _logger.LogWarning("Invalid profileId rejected: '{Profile}'", profileId);
            return false;
        }

        try
        {
            if (TryCliHotSwitch(profileId))
            {
                CurrentProfileId = profileId;
                return true;
            }

            return FallbackConfigSwap(profileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply FanControl profile '{Profile}'", profileId);
            return false;
        }
    }

    public bool ApplyCustomCurve(string jsonCurveData)
    {
        if (!IsSupported || _fanControlConfigDir == null) return false;

        try
        {
            var customPath = Path.Combine(_fanControlConfigDir, "custom_rog.json");
            var curveDoc = JsonDocument.Parse(jsonCurveData);
            
            var stub = new 
            { 
                name = "custom_rog", 
                description = "Custom fan curve from RogCustom API", 
                generatedBy = "RogCustom", 
                curvePoints = curveDoc.RootElement
            };
            
            var json = JsonSerializer.Serialize(stub, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(customPath, json);
            _logger.LogInformation("Generated custom FanControl profile from UI graph");
            
            return ApplyProfile("custom_rog");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply custom fan curve");
            return false;
        }
    }

    public bool RestoreDefaults()
    {
        if (_fanControlConfigDir == null || _backupConfigPath == null || !File.Exists(_backupConfigPath))
            return true;

        try
        {
            var activeConfig = Path.Combine(_fanControlConfigDir, "FanControl.json");
            File.Copy(_backupConfigPath, activeConfig, overwrite: true);
            _logger.LogInformation("Restored FanControl config from backup");
            RestartFanControl();
            CurrentProfileId = null;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore FanControl config");
            return false;
        }
    }

    private bool TryCliHotSwitch(string profileId)
    {
        if (_fanControlPath == null || !IsFanControlRunning()) return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _fanControlPath,
                Arguments = $"-c {profileId}.json",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(5000);

            if (proc.ExitCode == 0)
            {
                _logger.LogInformation("FanControl CLI hot-switch to '{Profile}' succeeded", profileId);
                return true;
            }

            _logger.LogWarning("FanControl CLI hot-switch failed (exit {Code}), falling back to config swap", proc.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FanControl CLI hot-switch unavailable, falling back to config swap");
            return false;
        }
    }

    private bool FallbackConfigSwap(string profileId)
    {
        if (_fanControlConfigDir == null) return false;

        var profileConfig = Path.Combine(_fanControlConfigDir, $"{profileId}.json");
        var activeConfig = Path.Combine(_fanControlConfigDir, "FanControl.json");

        if (!File.Exists(profileConfig))
        {
            _logger.LogWarning("FanControl profile config not found: {Path}", profileConfig);
            return false;
        }

        if (_backupConfigPath == null && File.Exists(activeConfig))
        {
            _backupConfigPath = activeConfig + ".bak";
            File.Copy(activeConfig, _backupConfigPath, overwrite: true);
            _logger.LogInformation("Backed up FanControl config to {Path}", _backupConfigPath);
        }

        File.Copy(profileConfig, activeConfig, overwrite: true);
        _logger.LogInformation("Applied FanControl profile '{Profile}' via config swap", profileId);

        RestartFanControl();
        CurrentProfileId = profileId;
        return true;
    }

    private void EnsureProfileStubs()
    {
        if (_fanControlConfigDir == null) return;

        foreach (var kvp in DefaultProfileDescriptions)
        {
            var profilePath = Path.Combine(_fanControlConfigDir, $"{kvp.Key}.json");
            if (File.Exists(profilePath)) continue;

            try
            {
                var stub = new { name = kvp.Key, description = kvp.Value, generatedBy = "RogCustom" };
                var json = JsonSerializer.Serialize(stub, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profilePath, json);
                _logger.LogInformation("Generated FanControl profile stub: {Path}", profilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create FanControl profile stub: {Path}", profilePath);
            }
        }
    }

    private void DetectInstallation()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FanControl"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FanControl"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FanControl"),
            @"C:\FanControl",
        };

        foreach (var dir in candidates)
        {
            var exe = Path.Combine(dir, "FanControl.exe");
            if (File.Exists(exe))
            {
                _fanControlPath = exe;
                _fanControlConfigDir = dir;
                _logger.LogInformation("FanControl detected at {Path}", dir);
                return;
            }
        }

        _logger.LogInformation("FanControl not detected on this system");
    }

    private bool IsFanControlRunning()
    {
        try
        {
            return Process.GetProcessesByName("FanControl").Length > 0;
        }
        catch { return false; }
    }

    private void RestartFanControl()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("FanControl"))
            {
                proc.CloseMainWindow();
                if (!proc.WaitForExit(3000))
                    proc.Kill();
            }

            if (_fanControlPath != null && File.Exists(_fanControlPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _fanControlPath,
                    UseShellExecute = true,
                });
                _logger.LogInformation("Restarted FanControl");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart FanControl");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RestoreDefaults();
    }
}
