using Avalonia.Controls;
using Avalonia.Interactivity;

using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Views.Settings;

public partial class SecretSettingView : UserControl
{
    public SecretSettingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (DataContext is SecretSettingViewModel viewModel
            && viewModel.LoadCommand.CanExecute(null))
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
        }
    }
}
