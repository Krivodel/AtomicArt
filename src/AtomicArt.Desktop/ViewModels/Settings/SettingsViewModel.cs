using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class SettingsViewModel :
    ObservableObject,
    IRecipient<LocalizationChangedMessage>,
    IDisposable
{
    public IReadOnlyList<ISettingItemViewModel> Settings { get; }
    public IReadOnlyList<SettingsGroupViewModel> Groups { get; }

    public event EventHandler? CloseRequested;

    public SettingsViewModel(
        ISettingsItemViewModelProvider settingsItemViewModelProvider,
        ILocalizationTextProvider textProvider,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(settingsItemViewModelProvider);
        ArgumentNullException.ThrowIfNull(textProvider);
        ArgumentNullException.ThrowIfNull(messenger);

        Settings = settingsItemViewModelProvider.CreateSettings();
        Groups = Settings
            .GroupBy(setting => setting.Section)
            .OrderBy(group => group.Key.Order)
            .ThenBy(group => group.Key.Key, StringComparer.Ordinal)
            .Select(group => new SettingsGroupViewModel(
                group.Key,
                group
                    .OrderBy(setting => setting.Order)
                    .ThenBy(setting => setting.Key, StringComparer.Ordinal)
                    .ToList(),
                textProvider))
            .ToList();
        messenger.Register<LocalizationChangedMessage>(this);
    }

    public void Receive(LocalizationChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (ISettingItemViewModel setting in Settings)
        {
            setting.RefreshLocalization();
        }

        foreach (SettingsGroupViewModel group in Groups)
        {
            group.RefreshLocalization();
        }
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
