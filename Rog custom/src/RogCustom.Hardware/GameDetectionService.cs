using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RogCustom.Core;

namespace RogCustom.Hardware;

public interface IGameDetectionService
{
    bool IsAutoSwitchingEnabled { get; set; }
}

public sealed class GameDetectionService : IGameDetectionService, IDisposable
{
    private readonly ILogger<GameDetectionService> _logger;
    private readonly IModeOrchestrator _orchestrator;
    private readonly CancellationTokenSource _cts;
    private bool _isGameRunning;
    
    public bool IsAutoSwitchingEnabled { get; set; } = false;

    // A small list of known popular game executables for demonstration
    private readonly HashSet<string> _knownGames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2", "valorant", "r5apex", "overwatch", "dota2", "leagueoflegends",
        "cyberpunk2077", "witcher3", "rdr2", "gta5", "eldenring", "bg3",
        "helldivers2", "palworld", "forza_horizon_5"
    };

    public GameDetectionService(ILogger<GameDetectionService> logger, IModeOrchestrator orchestrator)
    {
        _logger = logger;
        _orchestrator = orchestrator;
        _cts = new CancellationTokenSource();
        Task.Run(() => MonitorLoopAsync(_cts.Token));
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (IsAutoSwitchingEnabled)
                {
                    bool gameFound = CheckForGames();

                    if (gameFound && !_isGameRunning)
                    {
                        _isGameRunning = true;
                        _logger.LogInformation("Game detected! Switching to Performance mode.");
                        _orchestrator.ApplyMode(PerformanceMode.Performance);
                    }
                    else if (!gameFound && _isGameRunning)
                    {
                        _isGameRunning = false;
                        _logger.LogInformation("Game closed. Reverting to Windows mode.");
                        _orchestrator.ApplyMode(PerformanceMode.Windows);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error in game detection loop");
            }

            await Task.Delay(5000, token); // Poll every 5 seconds
        }
    }

    private bool CheckForGames()
    {
        try
        {
            var processes = Process.GetProcesses();
            bool found = false;
            foreach (var p in processes)
            {
                if (!found && _knownGames.Contains(p.ProcessName))
                {
                    found = true;
                }
                p.Dispose();
            }
            return found;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
