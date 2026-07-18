namespace AtomicArt.Desktop.ViewModels.Settings;

public interface ISettingsItemViewModelProvider
{
    IReadOnlyList<ISettingItemViewModel> CreateSettings();
}
