using System.Windows;
using System.Windows.Controls;

namespace RogCustom.App.Views;

public partial class DiagnosticsView : UserControl
{
    private readonly ViewModels.DiagnosticsViewModel _vm;

    public DiagnosticsView(ViewModels.DiagnosticsViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _vm.Refresh();

    private void Rebind_Click(object sender, RoutedEventArgs e) => _vm.Rebind();
}
