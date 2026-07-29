using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public interface ISettingItemViewModel
{
    string DisplayName { get; }
    string? ErrorMessage { get; }
    bool HasErrorMessage { get; }
    string Key { get; }
    int Order { get; }
    SettingsSection Section { get; }

    void RefreshLocalization();
}
