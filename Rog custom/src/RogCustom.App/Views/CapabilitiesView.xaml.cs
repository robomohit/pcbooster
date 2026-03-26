using System.Windows;
using System.Windows.Controls;

namespace RogCustom.App.Views;

public partial class CapabilitiesView : UserControl
{
    private ViewModels.CapabilitiesViewModel? _vm;

    public CapabilitiesView()
    {
        InitializeComponent();
    }

    public CapabilitiesView(ViewModels.CapabilitiesViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        InitializeComponent();
    }

    private void CapabilitiesView_Loaded(object sender, RoutedEventArgs e)
    {
        TryBind();
    }

    private void TryBind()
    {
        if (DataContext is ViewModels.CapabilitiesViewModel vm)
            _vm = vm;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _vm?.Refresh();
    }
}
