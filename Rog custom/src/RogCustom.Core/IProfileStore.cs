namespace RogCustom.Core;

public interface IProfileStore
{
    PerformanceProfile Load();
    void Save(PerformanceProfile profile);
    Guid? GetGuidForMode(PerformanceMode mode);
    void SetGuidForMode(PerformanceMode mode, Guid guid);
    ModeSettings GetModeSettings(PerformanceMode mode);
    void SetModeSettings(PerformanceMode mode, ModeSettings settings);
    List<string> GetProfileNames();
    void SetActiveProfile(string name);
    void CreateProfile(string name);
    void DeleteProfile(string name);
}
