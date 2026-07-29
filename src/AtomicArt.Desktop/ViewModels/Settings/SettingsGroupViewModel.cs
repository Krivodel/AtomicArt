using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SettingsGroupViewModel : ObservableObject
{
    public string Title => _textProvider.Get(_section.DisplayNameKey);
    public IReadOnlyList<ISettingItemViewModel> Settings { get; }

    private readonly SettingsSection _section;
    private readonly ILocalizationTextProvider _textProvider;

    public SettingsGroupViewModel(
        SettingsSection section,
        IReadOnlyList<ISettingItemViewModel> settings,
        ILocalizationTextProvider textProvider)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(textProvider);

        _section = section;
        _textProvider = textProvider;
        Settings = settings;
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
    }
}
