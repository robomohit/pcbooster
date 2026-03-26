using System;
using System.Collections.Generic;

namespace RogCustom.Hardware;

public class PerformanceProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Profile";
    public int? CoreOffsetMhz { get; set; }
    public int? MemOffsetMhz { get; set; }
    public float? PowerLimitWatts { get; set; }
    public string? FanCurveJson { get; set; }
}

public interface IProfileManagerService
{
    IEnumerable<PerformanceProfile> GetProfiles();
    PerformanceProfile? GetProfile(string id);
    void SaveProfile(PerformanceProfile profile);
    void DeleteProfile(string id);
    bool ApplyProfile(string id);
}
