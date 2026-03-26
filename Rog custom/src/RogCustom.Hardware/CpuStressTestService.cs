using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RogCustom.Core;

namespace RogCustom.Hardware;

public sealed class CpuStressTestService : ICpuStressTestService, IDisposable
{
    private readonly ILogger<CpuStressTestService> _logger;
    private readonly IHardwareMonitor _monitor;
    private CancellationTokenSource? _cts;
    private const int ThrottleThresholdC = 95;

    public bool IsStressing { get; private set; }
    public int ProgressPercent { get; private set; }
    public string? StatusMessage { get; private set; }
    public float? MaxCpuTempRecorded { get; private set; }
    public float? MinCpuTempRecorded { get; private set; }
    public float? AvgCpuTempRecorded { get; private set; }
    public float? MaxCpuPowerRecorded { get; private set; }
    public float? AvgCpuPowerRecorded { get; private set; }
    public float? MaxCpuClockRecorded { get; private set; }
    public float? AvgCpuClockRecorded { get; private set; }
    public int ThrottleEventCount { get; private set; }
    public int? LastCpuScore { get; private set; }
    public string? StabilityGrade { get; private set; }

    public CpuStressTestService(ILogger<CpuStressTestService> logger, IHardwareMonitor monitor)
    {
        _logger = logger;
        _monitor = monitor;
    }

    public void StartStressTest(int durationSeconds, int maxTempLimitC)
    {
        if (IsStressing) return;

        IsStressing = true;
        ProgressPercent = 0;
        MaxCpuTempRecorded = null;
        MinCpuTempRecorded = null;
        AvgCpuTempRecorded = null;
        MaxCpuPowerRecorded = null;
        AvgCpuPowerRecorded = null;
        MaxCpuClockRecorded = null;
        AvgCpuClockRecorded = null;
        ThrottleEventCount = 0;
        LastCpuScore = null;
        StabilityGrade = null;
        StatusMessage = "Initializing CPU Stress Test...";

        _cts = new CancellationTokenSource();
        Task.Run(() => RunStressTestAsync(durationSeconds, maxTempLimitC, _cts.Token));
    }

    public void CancelStressTest()
    {
        if (!IsStressing) return;
        _cts?.Cancel();
    }

    private async Task RunStressTestAsync(int totalSeconds, int maxTempLimit, CancellationToken token)
    {
        StreamWriter? logWriter = null;
        var workerCts = CancellationTokenSource.CreateLinkedTokenSource(token);

        try
        {
            // Start CSV logging
            var logPath = Path.Combine(
                ConfigPathHelper.GetConfigDirectory(),
                $"cpu_stresstest_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            logWriter = new StreamWriter(logPath);
            await logWriter.WriteLineAsync("Time,CpuTemp,CpuClock,CpuUsage,CpuPower,CpuFanRpm");

            int threadCount = Environment.ProcessorCount;
            StatusMessage = $"Spawning {threadCount}-thread CPU workload...";
            _logger.LogInformation("Starting CPU stress test: {Threads} threads, {Duration}s, kill-switch at {MaxTemp}C",
                threadCount, totalSeconds, maxTempLimit);

            // Launch CPU stress worker threads (heavy prime + matrix computation)
            var workers = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                workers[t] = Task.Run(() => CpuBurnWorker(workerCts.Token), workerCts.Token);
            }

            float totalTemp = 0f;
            float totalPower = 0f;
            float totalClock = 0f;
            int sampleCount = 0;
            float baselineClock = 0f;
            bool killedByThermal = false;

            for (int i = 0; i < totalSeconds; i++)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token);

                var snap = _monitor.GetLastSnapshot();
                float cpuTemp = snap.CpuPackageTemp ?? 0f;
                float cpuPower = snap.CpuPowerWatts ?? 0f;
                float cpuClock = snap.CpuEffectiveClockMHz ?? 0f;
                float cpuUsage = snap.CpuUsagePercent ?? 0f;
                float cpuFan = snap.CpuFanRpm ?? 0f;

                if (i == 0 && cpuClock > 0) baselineClock = cpuClock;

                sampleCount++;
                totalTemp += cpuTemp;
                totalPower += cpuPower;
                totalClock += cpuClock;

                // Track min/max
                if (MaxCpuTempRecorded == null || cpuTemp > MaxCpuTempRecorded) MaxCpuTempRecorded = cpuTemp;
                if (MinCpuTempRecorded == null || cpuTemp < MinCpuTempRecorded) MinCpuTempRecorded = cpuTemp;
                if (MaxCpuPowerRecorded == null || cpuPower > MaxCpuPowerRecorded) MaxCpuPowerRecorded = cpuPower;
                if (MaxCpuClockRecorded == null || cpuClock > MaxCpuClockRecorded) MaxCpuClockRecorded = cpuClock;

                // Log to CSV
                if (logWriter != null)
                {
                    await logWriter.WriteLineAsync($"{i},{cpuTemp:F1},{cpuClock:F0},{cpuUsage:F1},{cpuPower:F1},{cpuFan:F0}");
                }

                // Thermal Kill-Switch
                if (cpuTemp >= maxTempLimit)
                {
                    StatusMessage = $"THERMAL KILL-SWITCH at {cpuTemp:F1}C! Test aborted for safety.";
                    _logger.LogCritical("CPU stress test thermal kill-switch triggered at {Temp}C", cpuTemp);
                    killedByThermal = true;
                    break;
                }

                // Throttle detection: temp above threshold OR clock drops >15% from baseline
                if (cpuTemp >= ThrottleThresholdC || (baselineClock > 0 && cpuClock < baselineClock * 0.85f))
                {
                    ThrottleEventCount++;
                    StatusMessage = $"THROTTLE DETECTED @ {cpuTemp:F1}C | Clock: {cpuClock:F0}MHz";
                    _logger.LogWarning("CPU throttle detected. Temp: {Temp}C, Clock: {Clock}MHz", cpuTemp, cpuClock);
                }
                else
                {
                    StatusMessage = $"CPU Burn Active... {cpuTemp:F1}C | {cpuClock:F0}MHz | {cpuPower:F1}W | {cpuUsage:F0}%";
                }

                ProgressPercent = (int)((i / (float)totalSeconds) * 100);
            }

            // Stop workers
            workerCts.Cancel();

            if (!killedByThermal)
                token.ThrowIfCancellationRequested();

            ProgressPercent = 100;

            // Calculate averages
            if (sampleCount > 0)
            {
                AvgCpuTempRecorded = totalTemp / sampleCount;
                AvgCpuPowerRecorded = totalPower / sampleCount;
                AvgCpuClockRecorded = totalClock / sampleCount;
            }

            // Calculate CPU Score
            float avgUsage = sampleCount > 0 ? 100f : 0f; // Should be ~100% during burn
            float rawScore = ((AvgCpuClockRecorded ?? 0f) * 1.2f)
                           + (Environment.ProcessorCount * 150f)
                           + ((AvgCpuPowerRecorded ?? 0f) * 3.5f)
                           - ((MaxCpuTempRecorded ?? 0f) * 2.0f)
                           - (ThrottleEventCount * 250f);

            LastCpuScore = rawScore > 0 ? (int)rawScore : 0;

            // Calculate stability grade
            StabilityGrade = CalculateStabilityGrade(
                ThrottleEventCount, totalSeconds,
                MaxCpuTempRecorded ?? 0f, AvgCpuTempRecorded ?? 0f,
                killedByThermal);

            if (killedByThermal)
                StatusMessage = $"Aborted (thermal). Score: {LastCpuScore} | Grade: {StabilityGrade}";
            else if (ThrottleEventCount > 0)
                StatusMessage = $"Done with {ThrottleEventCount} throttle events. Score: {LastCpuScore} | Grade: {StabilityGrade}";
            else
                StatusMessage = $"Test Passed! Score: {LastCpuScore} | Grade: {StabilityGrade}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "CPU stress test cancelled.";
            ProgressPercent = 0;
            _logger.LogInformation("CPU stress test cancelled by user.");
        }
        catch (Exception ex)
        {
            StatusMessage = "CPU stress test failed with an error.";
            ProgressPercent = 0;
            _logger.LogError(ex, "CPU stress test failed.");
        }
        finally
        {
            IsStressing = false;
            workerCts.Cancel();
            workerCts.Dispose();

            if (logWriter != null)
            {
                await logWriter.FlushAsync();
                logWriter.Dispose();
            }
        }
    }

    /// <summary>
    /// Heavy computational workload that stresses all CPU cores.
    /// Uses a mix of prime sieve, matrix multiplication, and floating-point operations.
    /// </summary>
    private static void CpuBurnWorker(CancellationToken token)
    {
        // Use a combination of integer and floating-point workloads
        // to stress both the ALU and FPU pipelines
        var rng = new Random(Environment.CurrentManagedThreadId);
        const int matrixSize = 64;
        var matA = new double[matrixSize, matrixSize];
        var matB = new double[matrixSize, matrixSize];
        var matC = new double[matrixSize, matrixSize];

        // Initialize matrices
        for (int i = 0; i < matrixSize; i++)
            for (int j = 0; j < matrixSize; j++)
            {
                matA[i, j] = rng.NextDouble() * 100;
                matB[i, j] = rng.NextDouble() * 100;
            }

        while (!token.IsCancellationRequested)
        {
            // Matrix multiplication -- heavy FPU load
            for (int i = 0; i < matrixSize; i++)
            {
                for (int j = 0; j < matrixSize; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < matrixSize; k++)
                        sum += matA[i, k] * matB[k, j];
                    matC[i, j] = sum;
                }
                if (token.IsCancellationRequested) return;
            }

            // Prime sieve -- heavy integer/branch load
            int limit = 50000 + rng.Next(10000);
            bool[] sieve = new bool[limit];
            for (int i = 2; i * i < limit; i++)
            {
                if (!sieve[i])
                    for (int j = i * i; j < limit; j += i)
                        sieve[j] = true;
                if (token.IsCancellationRequested) return;
            }

            // Trigonometric chain -- stress FPU precision pipeline
            double val = rng.NextDouble();
            for (int i = 0; i < 100000; i++)
            {
                val = Math.Sin(val) * Math.Cos(val) + Math.Sqrt(Math.Abs(val) + 1.0);
                if (i % 10000 == 0 && token.IsCancellationRequested) return;
            }

            // Copy result back to prevent optimization elimination
            matA[0, 0] = matC[0, 0] + val;
        }
    }

    private static string CalculateStabilityGrade(
        int throttleEvents, int totalSeconds,
        float maxTemp, float avgTemp, bool thermalKill)
    {
        if (thermalKill) return "F";

        float throttleRatio = totalSeconds > 0 ? throttleEvents / (float)totalSeconds : 1f;

        if (throttleRatio == 0 && maxTemp < 85f) return "A+";
        if (throttleRatio == 0 && maxTemp < 90f) return "A";
        if (throttleRatio < 0.05f && maxTemp < 92f) return "B+";
        if (throttleRatio < 0.10f) return "B";
        if (throttleRatio < 0.20f) return "C";
        if (throttleRatio < 0.40f) return "D";
        return "F";
    }

    public void Dispose()
    {
        CancelStressTest();
    }
}
