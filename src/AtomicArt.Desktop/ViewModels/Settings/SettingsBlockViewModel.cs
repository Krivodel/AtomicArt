using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SettingsBlockViewModel : ObservableObject
{
    public IReadOnlyList<ISettingItemViewModel> Settings { get; }
    public string? Title => _section is null ? null : _textProvider.Get(_section.DisplayNameKey);
    public bool HasTitle => _section is not null;

    private readonly SettingsSection? _section;
    private readonly ILocalizationTextProvider _textProvider;

    public SettingsBlockViewModel(
        SettingsSection? section,
        IReadOnlyList<ISettingItemViewModel> settings,
        ILocalizationTextProvider textProvider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(textProvider);

        _section = section;
        _textProvider = textProvider;
        Settings = settings;
    }

    public void RefreshLocalization()
    {
        if (HasTitle)
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
