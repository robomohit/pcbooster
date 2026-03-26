namespace RogCustom.Hardware;

public interface IFanBridgeService
{
    bool IsSupported { get; }
    bool IsConnected { get; }
    bool ApplyProfile(string profileId);
    bool ApplyCustomCurve(string jsonCurveData);
    bool RestoreDefaults();
    string? CurrentProfileId { get; }
}
