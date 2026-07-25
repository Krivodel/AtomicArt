namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SettingsGroupViewModel
{
    public string Title { get; }
    public IReadOnlyList<ISettingItemViewModel> Settings { get; }

    public SettingsGroupViewModel(
        string title,
        IReadOnlyList<ISettingItemViewModel> settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(settings);

        Title = title;
        Settings = settings;
    }
}
