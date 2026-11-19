using CommunityToolkit.Mvvm.ComponentModel;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SettingsGroupViewModel : ObservableObject
{
    public string Title => _textProvider.Get(_section.DisplayNameKey);
    public IReadOnlyList<SettingsBlockViewModel> Blocks { get; }

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
        Blocks = CreateBlocks(settings, textProvider);
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));

        foreach (SettingsBlockViewModel block in Blocks)
        {
            block.RefreshLocalization();
        }
    }

    private static IReadOnlyList<SettingsBlockViewModel> CreateBlocks(
        IReadOnlyList<ISettingItemViewModel> settings,
        ILocalizationTextProvider textProvider)
    {
        List<SettingsBlockViewModel> blocks = [];
        IEnumerable<IGrouping<SettingsSection, ISettingItemViewModel>> groups = settings
            .GroupBy(setting => setting.Section)
            .OrderBy(group => group.Key.Order)
            .ThenBy(group => group.Key.Key, StringComparer.Ordinal);

        foreach (IGrouping<SettingsSection, ISettingItemViewModel> group in groups)
        {
            if (group.Key.Parent is null)
            {
                blocks.AddRange(group.Select(setting =>
                    new SettingsBlockViewModel(null, new ISettingItemViewModel[] { setting }, textProvider)));
            }
            else
            {
                blocks.Add(new SettingsBlockViewModel(group.Key, group.ToArray(), textProvider));
            }
        }

        return blocks;
    }
}
