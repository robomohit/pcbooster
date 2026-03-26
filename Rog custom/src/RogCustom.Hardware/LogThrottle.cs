namespace RogCustom.Hardware;

/// <summary>
/// Rate-limit repeated log messages to prevent log spam from recurring telemetry errors.
/// Used in HardwareMonitor worker loop and ConsolePoC.
/// </summary>
internal sealed class LogThrottle
{
    private readonly TimeSpan _interval;
    private readonly Dictionary<string, DateTime> _lastLog = new();
    private readonly object _lock = new();

    public LogThrottle(TimeSpan interval) => _interval = interval;

    public bool ShouldLog(string key)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_lastLog.TryGetValue(key, out var last) && (now - last) < _interval)
                return false;
            _lastLog[key] = now;
            return true;
        }
    }
}
