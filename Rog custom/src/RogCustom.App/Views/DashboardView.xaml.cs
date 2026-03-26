using System.Windows;
using System.Windows.Controls;
using RogCustom.Core;

namespace RogCustom.App.Views;

public partial class DashboardView : UserControl
{
    private readonly ViewModels.DashboardViewModel _vm;

    public DashboardView(ViewModels.DashboardViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;
    }

    private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
    {
        _vm.Dispose();
    }

    private void ModeSilent_Click(object sender, RoutedEventArgs e) => _vm.SetMode(PerformanceMode.Silent);
    private void ModeWindows_Click(object sender, RoutedEventArgs e) => _vm.SetMode(PerformanceMode.Windows);
    private void ModeBalanced_Click(object sender, RoutedEventArgs e) => _vm.SetMode(PerformanceMode.Balanced);
    private void ModePerformance_Click(object sender, RoutedEventArgs e) => _vm.SetMode(PerformanceMode.Performance);
    private void ModeTurbo_Click(object sender, RoutedEventArgs e) => _vm.SetMode(PerformanceMode.Turbo);
    private void ModeManual_Click(object sender, RoutedEventArgs e) => _vm.SetMode(PerformanceMode.Manual);
    private void RefreshSensors_Click(object sender, RoutedEventArgs e) => _vm.RefreshSensors();
}
