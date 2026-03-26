using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RogCustom.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(ViewModels.SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = RogCustom.Core.ConfigPathHelper.GetConfigDirectory();
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { }
    }
}
