using Microsoft.Extensions.Logging;
using RogCustom.Core;

namespace RogCustom.Hardware;

public sealed class ModeOrchestrator : IModeOrchestrator
{
    private readonly ILogger<ModeOrchestrator> _logger;
    private readonly IProfileStore _profileStore;
    private readonly IPowerPlanService _powerPlan;
    private readonly ICpuBoostService _cpuBoost;
    private readonly IFanBridgeService _fanBridge;
    private readonly IGpuControlService _gpuControl;
    private readonly IAppCapabilitiesService _capabilities;

    public ModeOrchestrator(
        ILogger<ModeOrchestrator> logger,
        IProfileStore profileStore,
        IPowerPlanService powerPlan,
        ICpuBoostService cpuBoost,
        IFanBridgeService fanBridge,
        IGpuControlService gpuControl,
        IAppCapabilitiesService capabilities)
    {
        _logger = logger;
        _profileStore = profileStore;
        _powerPlan = powerPlan;
        _cpuBoost = cpuBoost;
        _fanBridge = fanBridge;
        _gpuControl = gpuControl;
        _capabilities = capabilities;
    }

    public bool ApplyMode(PerformanceMode mode)
    {
        try
        {
            if (mode == PerformanceMode.Windows)
            {
                var profile = _profileStore.Load();
                profile.LastActiveMode = mode;
                _profileStore.Save(profile);
                _capabilities.ClearLastError();
                _logger.LogInformation("Windows mode applied (no system changes)");
                return true;
            }

            var settings = _profileStore.GetModeSettings(mode);

            if (!string.IsNullOrWhiteSpace(settings.PowerPlanGuid) &&
                Guid.TryParse(settings.PowerPlanGuid, out var planGuid))
            {
                if (!_powerPlan.SetActiveScheme(planGuid))
                {
                    _capabilities.SetLastError($"Failed to set power plan for {mode}.");
                    _logger.LogWarning("Power plan change failed for mode {Mode}", mode);
                }
            }
            else
            {
                var fallbackGuid = _profileStore.GetGuidForMode(mode);
                if (fallbackGuid != null)
                    _powerPlan.SetActiveScheme(fallbackGuid.Value);
            }

            if (!_cpuBoost.SetBoostPolicy(settings.CpuBoost))
                _logger.LogWarning("CPU boost policy change failed for mode {Mode}", mode);

            if (!_cpuBoost.SetMaxProcessorState(settings.MaxProcessorStatePercent))
                _logger.LogWarning("Max processor state change failed for mode {Mode}", mode);

            if (!_cpuBoost.SetCoreParking(settings.CoreParking))
                _logger.LogWarning("Core parking toggle failed for mode {Mode}", mode);

            if (!string.IsNullOrWhiteSpace(settings.FanCurveId))
            {
                if (!_fanBridge.ApplyProfile(settings.FanCurveId))
                    _logger.LogWarning("Fan profile '{Profile}' apply failed for mode {Mode}", settings.FanCurveId, mode);
            }

            if (settings.GpuPowerLimitWatts.HasValue && _gpuControl.IsSupported)
            {
                if (!_gpuControl.SetPowerLimit(settings.GpuPowerLimitWatts.Value))
                    _logger.LogWarning("GPU power limit change failed for mode {Mode}", mode);
            }
            else if (_gpuControl.IsSupported && mode == PerformanceMode.Turbo)
            {
                // Auto-max power limit down to hardware max on Turbo
                if (_gpuControl.MaxPowerLimitWatts.HasValue)
                {
                    _gpuControl.SetPowerLimit(_gpuControl.MaxPowerLimitWatts.Value);
                    _logger.LogInformation("Turbo Mode: Maxed GPU power limit to {Max}W", _gpuControl.MaxPowerLimitWatts.Value);
                }
            }
            else if (_gpuControl.IsSupported && mode != PerformanceMode.Manual)
            {
                _gpuControl.RestoreDefaultPowerLimit();
                _logger.LogInformation("Mode {Mode}: Restored GPU power limit to default (100%)", mode);
            }

            // Persist the active mode without a redundant full reload from disk.
            // GetModeSettings() above already loaded the profile into memory.
            var currentProfile = _profileStore.Load();
            currentProfile.LastActiveMode = mode;
            _profileStore.Save(currentProfile);

            _capabilities.ClearLastError();
            _logger.LogInformation("Mode {Mode} applied: boost={Boost}, maxCpu={MaxCpu}%, fan={Fan}, gpuPl={GpuPl}",
                mode, settings.CpuBoost, settings.MaxProcessorStatePercent, settings.FanCurveId, settings.GpuPowerLimitWatts);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApplyMode failed");
            _capabilities.SetLastError(ex.Message);
            return false;
        }
    }
}
