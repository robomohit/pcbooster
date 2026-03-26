using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace RogCustom.App.ViewModels;

public sealed class AboutViewModel : INotifyPropertyChanged
{
    private string _version;
    private string _description;
    private string _troubleshooting;

    public AboutViewModel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        _version = assembly.GetName().Version?.ToString() ?? "Unknown";
        _description = "RogCustom - Armoury Crate Alternative for Generic Desktops\n\n" +
                     "Hardware monitoring via LibreHardwareMonitor\n" +
                     "Performance modes: Silent / Balanced / Performance / Turbo\n" +
                     "CPU boost policy control via powercfg\n" +
                     "Fan control via FanControl (Rem0o) integration\n" +
                     "Power plan switching via PowrProf\n" +
                     "Anti-cheat compatible with Limited Mode fallback";
        _troubleshooting = "TROUBLESHOOTING\n\n" +
                     "Sensors show '---':\n" +
                     "  - Run as Administrator (LHM needs kernel driver)\n" +
                     "  - Check Diagnostics page for detected hardware\n" +
                     "  - Secure Boot may block the LHM driver\n\n" +
                     "Limited Mode active:\n" +
                     "  - The LHM kernel driver failed to load\n" +
                     "  - Anti-cheat software (Vanguard, EAC) may block it\n" +
                     "  - Power plan switching still works without the driver\n\n" +
                     "Fan control not working:\n" +
                     "  - Install FanControl (Rem0o) from GitHub\n" +
                     "  - Place profile configs in FanControl install directory\n" +
                     "  - FanControl must be running for profile swap to work\n\n" +
                     "Mode switch has no effect:\n" +
                     "  - Check Profiles page — assign power plans to each mode\n" +
                     "  - Click 'Restore Defaults' to auto-detect plans\n" +
                     "  - Verify with Task Manager that plan changed";
    }

    public string Version => _version;
    public string Description => _description;
    public string Troubleshooting => _troubleshooting;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
