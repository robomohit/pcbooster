using RogCustom.Core;

namespace RogCustom.Hardware;

public interface ICpuBoostService
{
    bool SetBoostPolicy(CpuBoostPolicy policy);
    bool SetCoreParking(bool enabled);
    bool SetMaxProcessorState(int percent);
}
