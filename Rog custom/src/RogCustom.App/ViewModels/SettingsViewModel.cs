using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RogCustom.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private string _configPath;
    private string _logPath;

    public SettingsViewModel()
    {
        _configPath = RogCustom.Core.ConfigPathHelper.GetConfigDirectory();
        _logPath = System.IO.Path.Combine(_configPath, "logs");
    }

    public string ConfigPath => _configPath;
    public string LogPath => _logPath;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
