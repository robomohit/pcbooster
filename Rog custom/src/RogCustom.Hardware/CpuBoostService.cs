using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RogCustom.Core;

namespace RogCustom.Hardware;

public sealed class CpuBoostService : ICpuBoostService
{
    private readonly ILogger<CpuBoostService> _logger;

    private static readonly Guid ProcessorSubgroup = new("54533251-82be-4824-96c1-47b60b740d00");
    private static readonly Guid BoostSetting = new("be337238-0d82-4146-a960-4f3749d470c7");
    private static readonly Guid CoreParkingMin = new("0cc5b647-c1df-4637-891a-dec35c318583");
    private static readonly Guid MaxProcessorState = new("bc5038f7-23e0-4960-96da-33abaf5935ec");

    public CpuBoostService(ILogger<CpuBoostService> logger)
    {
        _logger = logger;
    }

    public bool SetBoostPolicy(CpuBoostPolicy policy)
    {
        var value = (int)policy;
        return RunPowercfg($"/setacvalueindex scheme_current {ProcessorSubgroup} {BoostSetting} {value}")
            && RunPowercfg("/setactive scheme_current");
    }

    public bool SetCoreParking(bool enabled)
    {
        var minCores = enabled ? 5 : 100;
        return RunPowercfg($"/setacvalueindex scheme_current {ProcessorSubgroup} {CoreParkingMin} {minCores}")
            && RunPowercfg("/setactive scheme_current");
    }

    public bool SetMaxProcessorState(int percent)
    {
        percent = Math.Clamp(percent, 5, 100);
        return RunPowercfg($"/setacvalueindex scheme_current {ProcessorSubgroup} {MaxProcessorState} {percent}")
            && RunPowercfg("/setactive scheme_current");
    }

    private bool RunPowercfg(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            var stderrTask = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit(5000);
            if (proc.ExitCode != 0)
            {
                var stderr = stderrTask.GetAwaiter().GetResult();
                _logger.LogWarning("powercfg {Args} failed (exit {Code}): {Err}", args, proc.ExitCode, stderr);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "powercfg {Args} threw", args);
            return false;
        }
    }
}
