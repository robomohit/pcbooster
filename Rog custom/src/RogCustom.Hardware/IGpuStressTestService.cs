namespace RogCustom.Hardware;

public interface IGpuStressTestService
{
    bool IsStressing { get; }
    int ProgressPercent { get; }
    string? StatusMessage { get; }
    float? MaxGpuTempRecorded { get; }
    int? LastRigScore { get; }
    void StartStressTest(int durationSeconds, int maxTempLimitC);
    void CancelStressTest();
}

