using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public static class SettingsSections
{
    public static SettingsSection Connection { get; } = new SettingsSection(
        "connection",
        SettingsLocalizationKeys.Sections.Connection,
        100);
    public static SettingsSection Appearance { get; } = new SettingsSection(
        "appearance",
        SettingsLocalizationKeys.Sections.Appearance,
        200);
    public static SettingsSection StorageAndPerformance { get; } = new SettingsSection(
        "storage-and-performance",
        SettingsLocalizationKeys.Sections.StorageAndPerformance,
        300);
}
