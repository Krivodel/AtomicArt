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
            await viewModel.RefreshOptionsCommand.ExecuteAsync(null);
        }
    }
}
