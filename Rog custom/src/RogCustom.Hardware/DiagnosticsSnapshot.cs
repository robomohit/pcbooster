namespace RogCustom.Hardware;

public sealed record DiagnosticsSnapshot(
    DateTimeOffset Timestamp,
    bool IsLimitedMode,
    string? LastError,
    IReadOnlyList<DiagnosticsHardware> Hardware,
    IReadOnlyDictionary<DesiredSensorRole, BoundSensor?> Bindings)
{
    public static DiagnosticsSnapshot Empty { get; } = new(DateTimeOffset.MinValue, false, null, Array.Empty<DiagnosticsHardware>(), new Dictionary<DesiredSensorRole, BoundSensor?>());
}

public sealed record DiagnosticsHardware(
    string HardwareType,
    string HardwareName,
    IReadOnlyList<DiagnosticsSensor> Sensors,
    IReadOnlyList<DiagnosticsHardware> SubHardware);

public sealed record DiagnosticsSensor(
    string SensorType,
    string SensorName,
    float? Value);

public enum SensorStatus
{
    Unbound,
    BoundNoValue,
    BoundHasValue,
}

public sealed record BoundSensor(
    string HardwareType,
    string HardwareName,
    string SensorType,
    string SensorName,
    SensorStatus Status = SensorStatus.Unbound);
