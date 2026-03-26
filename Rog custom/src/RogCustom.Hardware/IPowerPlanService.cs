namespace RogCustom.Hardware;

/// <summary>
/// Wraps PowrProf for getting/setting active power scheme and enumerating schemes with friendly names.
/// All errors are handled gracefully; never throws to caller for capability detection.
/// </summary>
public interface IPowerPlanService
{
    /// <summary>Gets the currently active power scheme GUID, or null on failure.</summary>
    Guid? GetActiveSchemeGuid();

    /// <summary>Sets the active power scheme. Returns true on success.</summary>
    bool SetActiveScheme(Guid schemeGuid);

    /// <summary>Enumerates all power schemes with friendly names.</summary>
    IReadOnlyList<(Guid Guid, string FriendlyName)> EnumerateSchemes();
}
