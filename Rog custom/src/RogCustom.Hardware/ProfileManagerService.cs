using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RogCustom.Core;

namespace RogCustom.Hardware;

public class ProfileManagerService : IProfileManagerService
{
    // Use a separate file to avoid conflicting with ProfileStore's profiles.json
    private readonly string _profilesFilePath;
    private readonly IGpuControlService _gpuService;
    private readonly IFanBridgeService _fanService;
    private readonly ILogger<ProfileManagerService> _logger;
    private List<GpuProfile> _profiles = new();

    public ProfileManagerService(IGpuControlService gpuService, IFanBridgeService fanService, ILogger<ProfileManagerService> logger)
    {
        _gpuService = gpuService;
        _fanService = fanService;
        _logger = logger;
        
        string configDir = ConfigPathHelper.GetConfigDirectory();
        _profilesFilePath = Path.Combine(configDir, "gpu-profiles.json");
        
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_profilesFilePath))
            {
                string json = File.ReadAllText(_profilesFilePath);
                var loaded = JsonSerializer.Deserialize<List<GpuProfile>>(json);
                if (loaded != null) _profiles = loaded;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse GPU profiles JSON from disk.");
            _profiles = new List<GpuProfile>();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read GPU profiles file from disk.");
            _profiles = new List<GpuProfile>();
        }
    }

    private void SaveToDisk()
    {
        try
        {
            string json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_profilesFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write GPU profiles to disk.");
        }
    }

    public IEnumerable<GpuProfile> GetProfiles()
    {
        return _profiles.ToList();
    }

    public GpuProfile? GetProfile(string id)
    {
        return _profiles.FirstOrDefault(p => p.Id == id);
    }

    public void SaveProfile(GpuProfile profile)
    {
        var existing = _profiles.FirstOrDefault(p => p.Id == profile.Id);
        if (existing != null)
        {
            _profiles.Remove(existing);
        }
        _profiles.Add(profile);
        SaveToDisk();
    }

    public void DeleteProfile(string id)
    {
        _profiles.RemoveAll(p => p.Id == id);
        SaveToDisk();
    }

    public bool ApplyProfile(string id)
    {
        var profile = GetProfile(id);
        if (profile == null) return false;

        bool success = true;

        if (profile.CoreOffsetMhz.HasValue)
            success &= _gpuService.LockGpuClocks(profile.CoreOffsetMhz.Value);

        if (profile.MemOffsetMhz.HasValue)
            success &= _gpuService.LockMemoryClocks(profile.MemOffsetMhz.Value);

        if (profile.PowerLimitWatts.HasValue)
            success &= _gpuService.SetPowerLimit(profile.PowerLimitWatts.Value);

        if (!string.IsNullOrEmpty(profile.FanCurveJson))
            success &= _fanService.ApplyCustomCurve(profile.FanCurveJson);

        return success;
    }
}
