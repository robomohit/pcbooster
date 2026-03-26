using RogCustom.Core;

namespace RogCustom.Hardware;

public interface IModeOrchestrator
{
    bool ApplyMode(PerformanceMode mode);
}
