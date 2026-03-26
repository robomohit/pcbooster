using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RogCustom.Hardware;

/// <summary>
/// Wraps PowrProf interop. All PowrProf-related logging happens here (interop does not log).
/// On first failure (e.g. non-admin), sets PowerPlanControlAvailable = false and LastError; does not throw.
/// </summary>
public sealed class PowerPlanService : IPowerPlanService
{
    private readonly ILogger<PowerPlanService> _logger;
    private readonly IAppCapabilitiesService _capabilities;
    private bool _capabilityProbed;

    public PowerPlanService(ILogger<PowerPlanService> logger, IAppCapabilitiesService capabilities)
    {
        _logger = logger;
        _capabilities = capabilities;
    }

    public Guid? GetActiveSchemeGuid()
    {
        try
        {
            var (ptr, errorCode) = PowrProfInterop.GetActiveSchemeGuid();
            if (errorCode != PowrProfInterop.ERROR_SUCCESS)
            {
                ProbeCapability($"PowerGetActiveScheme failed: 0x{errorCode:X}");
                return null;
            }
            try
            {
                if (!ptr.HasValue || ptr.Value == IntPtr.Zero)
                    return null;
                return Marshal.PtrToStructure<Guid>(ptr.Value);
            }
            finally
            {
                if (ptr.HasValue && ptr.Value != IntPtr.Zero)
                    _ = PowrProfInterop.LocalFree(ptr.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerGetActiveScheme threw");
            ProbeCapability(ex.Message);
            return null;
        }
    }

    public bool SetActiveScheme(Guid schemeGuid)
    {
        try
        {
            uint err = PowrProfInterop.PowerSetActiveScheme(IntPtr.Zero, schemeGuid);
            if (err != PowrProfInterop.ERROR_SUCCESS)
            {
                _logger.LogWarning("PowerSetActiveScheme failed: 0x{Code:X}", err);
                ProbeCapability($"Power plan switch failed: 0x{err:X}");
                return false;
            }
            _capabilities.ClearLastError();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerSetActiveScheme threw");
            ProbeCapability(ex.Message);
            return false;
        }
    }

    public IReadOnlyList<(Guid Guid, string FriendlyName)> EnumerateSchemes()
    {
        var result = new List<(Guid, string)>();
        try
        {
            var (schemes, errorCode) = PowrProfInterop.EnumerateSchemeGuids();
            if (errorCode != PowrProfInterop.ERROR_SUCCESS)
            {
                _logger.LogWarning("PowerEnumerate failed: 0x{Code:X}", errorCode);
                ProbeCapability($"PowerEnumerate failed: 0x{errorCode:X}");
                return result;
            }
            if (schemes == null)
                return result;
            foreach (var guid in schemes)
            {
                var (name, nameErr) = PowrProfInterop.ReadFriendlyName(guid);
                if (nameErr != PowrProfInterop.ERROR_SUCCESS)
                    _logger.LogDebug("PowerReadFriendlyName for {Guid} failed: 0x{Code:X}", guid, nameErr);
                result.Add((guid, name ?? guid.ToString()));
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnumerateSchemes threw");
            ProbeCapability(ex.Message);
            return result;
        }
    }

    private void ProbeCapability(string message)
    {
        if (_capabilityProbed)
            return;
        _capabilityProbed = true;
        _capabilities.SetPowerPlanControlAvailable(false);
        _capabilities.SetLastError(message);
    }
}
