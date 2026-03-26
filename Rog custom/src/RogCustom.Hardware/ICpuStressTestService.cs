namespace RogCustom.Hardware;

public interface ICpuStressTestService
{
    bool IsStressing { get; }
    int ProgressPercent { get; }
    string? StatusMessage { get; }
    float? MaxCpuTempRecorded { get; }
    float? MinCpuTempRecorded { get; }
    float? AvgCpuTempRecorded { get; }
    float? MaxCpuPowerRecorded { get; }
    float? AvgCpuPowerRecorded { get; }
    float? MaxCpuClockRecorded { get; }
    float? AvgCpuClockRecorded { get; }
    int ThrottleEventCount { get; }
    int? LastCpuScore { get; }
    string? StabilityGrade { get; }
    void StartStressTest(int durationSeconds, int maxTempLimitC);
    void CancelStressTest();
}
