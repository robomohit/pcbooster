using System;
using System.Collections.Generic;

namespace RogCustom.Hardware;

/// <summary>
/// A GPU overclocking / fan profile (distinct from RogCustom.Core.PerformanceProfile
/// which handles system-wide mode settings and schema-versioned persistence).
/// </summary>
public class GpuProfile
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
    IEnumerable<GpuProfile> GetProfiles();
    GpuProfile? GetProfile(string id);
    void SaveProfile(GpuProfile profile);
    void DeleteProfile(string id);
    bool ApplyProfile(string id);
}
