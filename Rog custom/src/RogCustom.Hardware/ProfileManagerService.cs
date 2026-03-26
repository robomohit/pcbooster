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
    private readonly string _profilesFilePath;
    private readonly IGpuControlService _gpuService;
    private readonly IFanBridgeService _fanService;
    private readonly ILogger<ProfileManagerService> _logger;
    private List<PerformanceProfile> _profiles = new();

    public ProfileManagerService(IGpuControlService gpuService, IFanBridgeService fanService, ILogger<ProfileManagerService> logger)
    {
        _gpuService = gpuService;
        _fanService = fanService;
        _logger = logger;
        
        string configDir = ConfigPathHelper.GetConfigDirectory();
        _profilesFilePath = Path.Combine(configDir, "profiles.json");
        
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_profilesFilePath))
            {
                string json = File.ReadAllText(_profilesFilePath);
                var loaded = JsonSerializer.Deserialize<List<PerformanceProfile>>(json);
                if (loaded != null) _profiles = loaded;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load performance profiles from disk.");
            _profiles = new List<PerformanceProfile>();
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
            _logger.LogError(ex, "Failed to write performance profiles to disk.");
        }
    }

    public IEnumerable<PerformanceProfile> GetProfiles()
    {
        return _profiles.ToList();
    }

    public PerformanceProfile? GetProfile(string id)
    {
        return _profiles.FirstOrDefault(p => p.Id == id);
    }

    public void SaveProfile(PerformanceProfile profile)
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
            success &= _gpuService.ApplyGpuCoreClockOffset(profile.CoreOffsetMhz.Value).IsSuccess;

        if (profile.MemOffsetMhz.HasValue)
            success &= _gpuService.ApplyGpuMemoryClockOffset(profile.MemOffsetMhz.Value).IsSuccess;

        if (profile.PowerLimitWatts.HasValue)
            success &= _gpuService.ApplyGpuPowerLimit(profile.PowerLimitWatts.Value).IsSuccess;

        if (!string.IsNullOrEmpty(profile.FanCurveJson))
            success &= _fanService.ApplyCustomCurve(profile.FanCurveJson);

        return success;
    }
}
