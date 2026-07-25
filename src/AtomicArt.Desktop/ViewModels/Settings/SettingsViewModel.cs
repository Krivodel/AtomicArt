using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    public IReadOnlyList<ISettingItemViewModel> Settings { get; }
    public IReadOnlyList<SettingsGroupViewModel> Groups { get; }

    public event EventHandler? CloseRequested;

    public SettingsViewModel(ISettingsItemViewModelProvider settingsItemViewModelProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsItemViewModelProvider);

        Settings = settingsItemViewModelProvider.CreateSettings();
        Groups = Settings
            .GroupBy(setting => setting.Section)
            .OrderBy(group => group.Key.Order)
            .ThenBy(group => group.Key.Key, StringComparer.Ordinal)
            .Select(group => new SettingsGroupViewModel(
                group.Key.DisplayName,
                group
                    .OrderBy(setting => setting.Order)
                    .ThenBy(setting => setting.Key, StringComparer.Ordinal)
                    .ToList()))
            .ToList();
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
