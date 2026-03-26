using System.Windows;
using System.Windows.Controls;

namespace RogCustom.App.Views;

public partial class FansView : UserControl
{
    private readonly ViewModels.FansViewModel _viewModel;

    public FansView(ViewModels.FansViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void QuietProfile_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyProfile("quiet");
    private void NormalProfile_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyProfile("normal");
    private void PerformanceProfile_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyProfile("performance");
    private void MaxProfile_Click(object sender, RoutedEventArgs e) => _viewModel.ApplyProfile("max");
    private void RestoreDefaults_Click(object sender, RoutedEventArgs e) => _viewModel.RestoreDefaults();
    private void Refresh_Click(object sender, RoutedEventArgs e) => _viewModel.Refresh();
}
