namespace RogCustom.Hardware;

public interface IGpuStressTestService
{
    bool IsStressing { get; }
    int ProgressPercent { get; }
    string? StatusMessage { get; }
    float? MaxGpuTempRecorded { get; }
    int? LastRigScore { get; }

    // Enhanced telemetry
    float? MinGpuTempRecorded { get; }
    float? AvgGpuTempRecorded { get; }
    float? MaxGpuPowerRecorded { get; }
    float? AvgGpuPowerRecorded { get; }
    int? MaxGpuClockRecorded { get; }
    int? AvgGpuClockRecorded { get; }
    float? MaxGpuFanRpmRecorded { get; }
    float? MaxVramUsageMb { get; }
    int ThrottleEventCount { get; }
    string? StabilityGrade { get; }

    void StartStressTest(int durationSeconds, int maxTempLimitC);
    void CancelStressTest();
}
