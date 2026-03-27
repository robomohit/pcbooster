using System.Text;

namespace RogCustom.Hardware;

/// <summary>
/// Heuristic AI analysis engine that examines stress test telemetry
/// and produces a detailed report with scores, findings, and recommendations.
/// </summary>
public static class StressTestAnalyzer
{
    public sealed class AnalysisReport
    {
        public string OverallVerdict { get; set; } = "";
        public string OverallGrade { get; set; } = "";
        public int OverallScore { get; set; }
        public List<string> Findings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public GpuAnalysis? Gpu { get; set; }
        public CpuAnalysis? Cpu { get; set; }
        public string DetailedReport { get; set; } = "";
    }

    public sealed class GpuAnalysis
    {
        public string ThermalVerdict { get; set; } = "";
        public string ClockStability { get; set; } = "";
        public string PowerEfficiency { get; set; } = "";
        public string CoolingAssessment { get; set; } = "";
        public string VramStatus { get; set; } = "";
        public int Score { get; set; }
        public string Grade { get; set; } = "";
    }

    public sealed class CpuAnalysis
    {
        public string ThermalVerdict { get; set; } = "";
        public string ClockStability { get; set; } = "";
        public string PowerAssessment { get; set; } = "";
        public string ThreadEfficiency { get; set; } = "";
        public int Score { get; set; }
        public string Grade { get; set; } = "";
    }

    public static AnalysisReport Analyze(
        IGpuStressTestService? gpuSvc,
        ICpuStressTestService? cpuSvc,
        IHardwareMonitor? monitor)
    {
        var report = new AnalysisReport();
        var findings = new List<string>();
        var recommendations = new List<string>();
        var sb = new StringBuilder();

        bool hasGpuData = gpuSvc?.LastRigScore != null;
        bool hasCpuData = cpuSvc?.LastCpuScore != null;

        if (!hasGpuData && !hasCpuData)
        {
            report.OverallVerdict = "No stress test data available. Run a stress test first.";
            report.OverallGrade = "?";
            report.DetailedReport = report.OverallVerdict;
            return report;
        }

        sb.AppendLine("=== RIGAI STRESS TEST ANALYSIS ===\n");

        // ── GPU Analysis ──
        if (hasGpuData && gpuSvc != null)
        {
            var gpu = AnalyzeGpu(gpuSvc, findings, recommendations);
            report.Gpu = gpu;

            sb.AppendLine("--- GPU ANALYSIS ---");
            sb.AppendLine($"Score: {gpu.Score} | Grade: {gpu.Grade}");
            sb.AppendLine($"Thermal: {gpu.ThermalVerdict}");
            sb.AppendLine($"Clocks: {gpu.ClockStability}");
            sb.AppendLine($"Power: {gpu.PowerEfficiency}");
            sb.AppendLine($"Cooling: {gpu.CoolingAssessment}");
            if (!string.IsNullOrEmpty(gpu.VramStatus))
                sb.AppendLine($"VRAM: {gpu.VramStatus}");
            sb.AppendLine();
        }

        // ── CPU Analysis ──
        if (hasCpuData && cpuSvc != null)
        {
            var cpu = AnalyzeCpu(cpuSvc, findings, recommendations);
            report.Cpu = cpu;

            sb.AppendLine("--- CPU ANALYSIS ---");
            sb.AppendLine($"Score: {cpu.Score} | Grade: {cpu.Grade}");
            sb.AppendLine($"Thermal: {cpu.ThermalVerdict}");
            sb.AppendLine($"Clocks: {cpu.ClockStability}");
            sb.AppendLine($"Power: {cpu.PowerAssessment}");
            sb.AppendLine($"Threads: {cpu.ThreadEfficiency}");
            sb.AppendLine();
        }

        // ── Overall Verdict ──
        int totalScore = 0;
        int parts = 0;
        if (report.Gpu != null) { totalScore += report.Gpu.Score; parts++; }
        if (report.Cpu != null) { totalScore += report.Cpu.Score; parts++; }
        report.OverallScore = parts > 0 ? totalScore / parts : 0;
        report.OverallGrade = ScoreToGrade(report.OverallScore);

        report.OverallVerdict = report.OverallScore switch
        {
            >= 90 => "Excellent system stability. Your hardware is performing at peak efficiency with no thermal or throttling concerns.",
            >= 75 => "Good system stability. Minor thermal or clock fluctuations detected but within acceptable ranges.",
            >= 60 => "Moderate stability. Some throttling detected -- review thermal management and consider improving airflow.",
            >= 40 => "Below average stability. Significant throttling or thermal issues detected. Action recommended.",
            _ => "Poor stability. Critical thermal or performance issues found. Immediate attention needed."
        };

        sb.AppendLine("--- OVERALL VERDICT ---");
        sb.AppendLine($"Score: {report.OverallScore}/100 | Grade: {report.OverallGrade}");
        sb.AppendLine(report.OverallVerdict);
        sb.AppendLine();

        if (findings.Count > 0)
        {
            sb.AppendLine("--- KEY FINDINGS ---");
            foreach (var f in findings) sb.AppendLine($"  * {f}");
            sb.AppendLine();
        }

        if (recommendations.Count > 0)
        {
            sb.AppendLine("--- RECOMMENDATIONS ---");
            foreach (var r in recommendations) sb.AppendLine($"  > {r}");
        }

        report.Findings = findings;
        report.Recommendations = recommendations;
        report.DetailedReport = sb.ToString();
        return report;
    }

    private static GpuAnalysis AnalyzeGpu(
        IGpuStressTestService svc,
        List<string> findings, List<string> recommendations)
    {
        var gpu = new GpuAnalysis
        {
            Score = svc.LastRigScore ?? 0,
            Grade = svc.StabilityGrade ?? "?"
        };

        float maxTemp = svc.MaxGpuTempRecorded ?? 0;
        float avgTemp = svc.AvgGpuTempRecorded ?? 0;
        float minTemp = svc.MinGpuTempRecorded ?? 0;
        float maxPower = svc.MaxGpuPowerRecorded ?? 0;
        float avgPower = svc.AvgGpuPowerRecorded ?? 0;
        int maxClock = svc.MaxGpuClockRecorded ?? 0;
        int avgClock = svc.AvgGpuClockRecorded ?? 0;
        float maxFan = svc.MaxGpuFanRpmRecorded ?? 0;
        float maxVram = svc.MaxVramUsageMb ?? 0;
        int throttles = svc.ThrottleEventCount;

        // Thermal analysis
        if (maxTemp < 70)
        {
            gpu.ThermalVerdict = $"Excellent thermals. Peak {maxTemp:F0}C is well within safe limits.";
        }
        else if (maxTemp < 80)
        {
            gpu.ThermalVerdict = $"Good thermals. Peak {maxTemp:F0}C with avg {avgTemp:F0}C. Normal operating range.";
        }
        else if (maxTemp < 88)
        {
            gpu.ThermalVerdict = $"Warm but acceptable. Peak {maxTemp:F0}C, avg {avgTemp:F0}C. Consider improving case airflow.";
            findings.Add($"GPU peaked at {maxTemp:F0}C during stress -- approaching thermal limits.");
            recommendations.Add("Ensure GPU fans are clean and case has adequate intake/exhaust airflow.");
        }
        else
        {
            gpu.ThermalVerdict = $"Hot! Peak {maxTemp:F0}C triggers throttling territory. Cooling intervention needed.";
            findings.Add($"GPU hit {maxTemp:F0}C -- this causes thermal throttling and reduced performance.");
            recommendations.Add("Repaste GPU thermal compound, increase fan curve aggressiveness, or add case fans.");
            recommendations.Add("Consider undervolting the GPU to reduce heat while maintaining clocks.");
        }

        // Clock stability
        if (maxClock > 0 && avgClock > 0)
        {
            float clockVariance = ((float)(maxClock - avgClock) / maxClock) * 100;
            if (clockVariance < 3)
            {
                gpu.ClockStability = $"Rock solid. Max {maxClock}MHz, avg {avgClock}MHz ({clockVariance:F1}% variance).";
            }
            else if (clockVariance < 8)
            {
                gpu.ClockStability = $"Stable. Max {maxClock}MHz, avg {avgClock}MHz ({clockVariance:F1}% variance). Minor boost fluctuation.";
            }
            else
            {
                gpu.ClockStability = $"Unstable. Max {maxClock}MHz but avg {avgClock}MHz ({clockVariance:F1}% variance). Clocks dropping under load.";
                findings.Add($"GPU clocks fluctuated {clockVariance:F0}% from peak -- indicates power or thermal limiting.");
            }
        }
        else
        {
            gpu.ClockStability = "Clock data unavailable.";
        }

        // Power efficiency
        if (maxPower > 0 && avgPower > 0)
        {
            float powerEffRatio = avgPower / maxPower;
            gpu.PowerEfficiency = powerEffRatio > 0.9f
                ? $"Consistent power draw. Peak {maxPower:F0}W, avg {avgPower:F0}W. GPU is using its full power budget."
                : $"Power fluctuating. Peak {maxPower:F0}W but avg only {avgPower:F0}W. GPU may be power-limited.";
            if (powerEffRatio < 0.8f)
                findings.Add("GPU average power significantly below peak -- may indicate intermittent power throttling.");
        }
        else
        {
            gpu.PowerEfficiency = "Power data unavailable.";
        }

        // Cooling assessment
        if (maxFan > 0)
        {
            gpu.CoolingAssessment = maxFan > 2500
                ? $"Fans reached {maxFan:F0}RPM -- running at high speed. Thermal headroom is tight."
                : maxFan > 1500
                    ? $"Fans reached {maxFan:F0}RPM -- moderate speed. Cooling is adequate."
                    : $"Fans only hit {maxFan:F0}RPM -- cooling system barely engaged. Excellent thermal headroom.";
        }
        else
        {
            gpu.CoolingAssessment = "Fan data unavailable.";
        }

        // VRAM
        if (maxVram > 0)
        {
            gpu.VramStatus = $"Peak VRAM usage: {maxVram:F0}MB during stress test.";
        }

        // Throttle events
        if (throttles > 0)
        {
            findings.Add($"GPU experienced {throttles} throttle events during the test.");
            if (throttles > 5)
                recommendations.Add("Frequent GPU throttling detected. Lower your overclock or improve cooling.");
        }

        // Normalize score to 0-100
        gpu.Score = Math.Clamp(NormalizeGpuScore(gpu.Score, maxTemp, throttles), 0, 100);
        gpu.Grade = ScoreToGrade(gpu.Score);

        return gpu;
    }

    private static CpuAnalysis AnalyzeCpu(
        ICpuStressTestService svc,
        List<string> findings, List<string> recommendations)
    {
        var cpu = new CpuAnalysis
        {
            Score = svc.LastCpuScore ?? 0,
            Grade = svc.StabilityGrade ?? "?"
        };

        float maxTemp = svc.MaxCpuTempRecorded ?? 0;
        float avgTemp = svc.AvgCpuTempRecorded ?? 0;
        float minTemp = svc.MinCpuTempRecorded ?? 0;
        float maxPower = svc.MaxCpuPowerRecorded ?? 0;
        float avgPower = svc.AvgCpuPowerRecorded ?? 0;
        float maxClock = svc.MaxCpuClockRecorded ?? 0;
        float avgClock = svc.AvgCpuClockRecorded ?? 0;
        int throttles = svc.ThrottleEventCount;
        int cores = Environment.ProcessorCount;

        // Thermal analysis
        if (maxTemp < 70)
        {
            cpu.ThermalVerdict = $"Cool and collected. Peak {maxTemp:F0}C under full {cores}-thread load. Excellent cooler.";
        }
        else if (maxTemp < 82)
        {
            cpu.ThermalVerdict = $"Normal thermals. Peak {maxTemp:F0}C, avg {avgTemp:F0}C under all-core load.";
        }
        else if (maxTemp < 92)
        {
            cpu.ThermalVerdict = $"Running warm. Peak {maxTemp:F0}C on {cores} threads. Thermal headroom is limited.";
            findings.Add($"CPU peaked at {maxTemp:F0}C under full load -- approaching thermal limits.");
            recommendations.Add("Check CPU cooler mounting pressure and thermal paste application.");
            recommendations.Add("Consider upgrading to a higher-capacity cooler if using the stock one.");
        }
        else
        {
            cpu.ThermalVerdict = $"Critically hot! {maxTemp:F0}C will cause throttling and potential longevity issues.";
            findings.Add($"CPU reached {maxTemp:F0}C -- thermal throttling is guaranteed at this temperature.");
            recommendations.Add("Immediately address CPU cooling: repaste, reseat cooler, or upgrade cooler.");
            recommendations.Add("Consider reducing CPU voltage via BIOS undervolt to lower temperatures.");
        }

        // Clock stability
        if (maxClock > 0 && avgClock > 0)
        {
            float clockDrop = ((maxClock - avgClock) / maxClock) * 100;
            if (clockDrop < 2)
                cpu.ClockStability = $"Perfectly stable. All {cores} threads held {avgClock:F0}MHz consistently.";
            else if (clockDrop < 5)
                cpu.ClockStability = $"Stable. Peak {maxClock:F0}MHz, avg {avgClock:F0}MHz. Minor boost variance.";
            else
            {
                cpu.ClockStability = $"Clocks dropping under load. Peak {maxClock:F0}MHz but avg {avgClock:F0}MHz ({clockDrop:F0}% drop).";
                findings.Add($"CPU clocks dropped {clockDrop:F0}% from peak during all-core stress.");
            }
        }
        else
        {
            cpu.ClockStability = "Clock data unavailable.";
        }

        // Power assessment
        if (maxPower > 0)
        {
            cpu.PowerAssessment = $"Peak power draw: {maxPower:F0}W, avg {avgPower:F0}W across {cores} threads.";
            if (maxPower > 150)
            {
                findings.Add($"CPU drew up to {maxPower:F0}W -- high power consumption. Ensure PSU has sufficient headroom.");
            }
        }
        else
        {
            cpu.PowerAssessment = "Power data unavailable.";
        }

        // Thread efficiency
        cpu.ThreadEfficiency = $"Stressed {cores} logical processors simultaneously with matrix/prime/trig workloads.";

        // Throttle events
        if (throttles > 0)
        {
            findings.Add($"CPU experienced {throttles} throttle events during the test.");
            if (throttles > 5)
                recommendations.Add("Frequent CPU throttling. Lower PBO/boost limits or improve cooling.");
        }

        // Normalize score
        cpu.Score = Math.Clamp(NormalizeCpuScore(cpu.Score, maxTemp, throttles, cores), 0, 100);
        cpu.Grade = ScoreToGrade(cpu.Score);

        return cpu;
    }

    private static int NormalizeGpuScore(int rawScore, float maxTemp, int throttles)
    {
        // Map raw RIG score (typically 0-3000+) to 0-100
        float base100 = Math.Min(rawScore / 25f, 80f);
        // Temp penalty
        if (maxTemp > 85) base100 -= (maxTemp - 85) * 2;
        if (maxTemp > 90) base100 -= (maxTemp - 90) * 3;
        // Throttle penalty
        base100 -= throttles * 3;
        // Bonus for low temps
        if (maxTemp < 70 && throttles == 0) base100 += 15;
        else if (maxTemp < 80 && throttles == 0) base100 += 8;

        return (int)Math.Clamp(base100, 0, 100);
    }

    private static int NormalizeCpuScore(int rawScore, float maxTemp, int throttles, int cores)
    {
        // Map raw CPU score to 0-100
        float base100 = Math.Min(rawScore / (cores * 5f), 80f);
        if (maxTemp > 85) base100 -= (maxTemp - 85) * 1.5f;
        if (maxTemp > 92) base100 -= (maxTemp - 92) * 3;
        base100 -= throttles * 3;
        if (maxTemp < 70 && throttles == 0) base100 += 15;
        else if (maxTemp < 80 && throttles == 0) base100 += 8;

        return (int)Math.Clamp(base100, 0, 100);
    }

    private static string ScoreToGrade(int score) => score switch
    {
        >= 95 => "A+",
        >= 85 => "A",
        >= 78 => "B+",
        >= 70 => "B",
        >= 60 => "C",
        >= 45 => "D",
        _ => "F"
    };
}
