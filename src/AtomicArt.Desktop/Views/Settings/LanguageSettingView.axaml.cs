using Avalonia.Controls;

using AtomicArt.Desktop.ViewModels.Settings;

namespace AtomicArt.Desktop.Views.Settings;

public partial class LanguageSettingView : UserControl
{
    public LanguageSettingView()
    {
        InitializeComponent();
    }

    private async void OnDropDownOpened(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (DataContext is LanguageSettingViewModel viewModel)
        {
            viewModel.ClearSearchCommand.Execute(null);
            await viewModel.RefreshOptionsCommand.ExecuteAsync(null);
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (DataContext is LanguageSettingViewModel viewModel)
        {
            viewModel.ClearSearchCommand.Execute(null);
        }
    }
}
