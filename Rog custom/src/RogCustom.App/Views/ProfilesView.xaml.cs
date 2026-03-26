using System.Windows;
using System.Windows.Controls;

namespace RogCustom.App.Views;

public partial class ProfilesView : UserControl
{
    private readonly ViewModels.ProfilesViewModel _viewModel;

    public ProfilesView(ViewModels.ProfilesViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => _viewModel.SaveProfile();
    private void RestoreDefaults_Click(object sender, RoutedEventArgs e) => _viewModel.RestoreDefaults();
    private void CreateProfile_Click(object sender, RoutedEventArgs e) => _viewModel.CreateNewProfile();
    private void DeleteProfile_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteSelectedProfile();
}
