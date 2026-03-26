namespace RogCustom.Hardware;

public sealed class StubFanBridgeService : IFanBridgeService
{
    public bool IsSupported => false;
    public bool IsConnected => false;
    public string? CurrentProfileId => null;
    public bool ApplyProfile(string profileId) => false;
    public bool ApplyCustomCurve(string jsonCurveData) => false;
    public bool RestoreDefaults() => true;
}
