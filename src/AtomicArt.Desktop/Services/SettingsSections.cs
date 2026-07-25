using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public static class SettingsSections
{
    public static SettingsSection Connection { get; } = new SettingsSection(
        "connection",
        UiStrings.SettingsConnectionSection,
        100);
    public static SettingsSection Appearance { get; } = new SettingsSection(
        "appearance",
        UiStrings.SettingsAppearanceSection,
        200);
    public static SettingsSection StorageAndPerformance { get; } = new SettingsSection(
        "storage-and-performance",
        UiStrings.SettingsStorageAndPerformanceSection,
        300);
}
