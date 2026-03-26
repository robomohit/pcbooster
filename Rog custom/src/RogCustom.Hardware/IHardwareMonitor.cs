namespace RogCustom.Hardware;

/// <summary>
/// Exposes the last hardware snapshot (read-only) and allows requesting a sensor rebind.
/// Snapshot is updated by the worker thread only. RequestRebind is asynchronous (handled by worker).
/// </summary>
public interface IHardwareMonitor
{
    /// <summary>Gets the most recent snapshot. May be Empty if LHM is in Limited Mode or not yet updated.</summary>
    HardwareSnapshot GetLastSnapshot();

    /// <summary>
    /// Gets the most recent diagnostics snapshot (raw sensors + current role bindings). May be empty until first refresh.
    /// </summary>
    DiagnosticsSnapshot GetDiagnosticsSnapshot();

    /// <summary>Request that the worker thread re-resolve sensor bindings. Non-blocking; never call LHM on UI thread.</summary>
    void RequestRebind();

    /// <summary>
    /// Request that the worker thread refresh the diagnostics snapshot (raw sensor list + binding summary).
    /// </summary>
    void RequestDiagnosticsRefresh();

    /// <summary>True if the monitor is running in Limited Mode (Power Plan only; LHM driver failed to load).</summary>
    bool IsLimitedMode { get; }
}
