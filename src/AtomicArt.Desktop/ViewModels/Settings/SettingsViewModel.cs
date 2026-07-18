using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    public IReadOnlyList<ISettingItemViewModel> Settings { get; }

    public event EventHandler? CloseRequested;

    public SettingsViewModel(ISettingsItemViewModelProvider settingsItemViewModelProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsItemViewModelProvider);

        Settings = settingsItemViewModelProvider.CreateSettings();
    }

    public void Dispose()
    {
        foreach (IDisposable setting in Settings.OfType<IDisposable>())
        {
            setting.Dispose();
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
