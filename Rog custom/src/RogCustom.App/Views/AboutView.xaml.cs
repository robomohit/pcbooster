using System.Windows.Controls;

namespace RogCustom.App.Views;

public partial class AboutView : UserControl
{
    public AboutView(ViewModels.AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Debug: Log that view is being created
        System.Diagnostics.Debug.WriteLine("AboutView created with DataContext");
    }
}
