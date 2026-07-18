using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public interface ISettingsItemViewModelFactory
{
    bool CanCreate(ISettingsDefinition definition);
    ISettingItemViewModel Create(ISettingsDefinition definition);
}
