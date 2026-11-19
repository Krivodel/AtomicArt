using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public static class SettingsSections
{
    public static SettingsSection Connection { get; } = new SettingsSection(
        "connection",
        SettingsLocalizationKeys.Sections.Connection,
        100);
    public static SettingsSection Server { get; } = new SettingsSection(
        "server",
        SettingsLocalizationKeys.Connection.Server,
        50,
        Connection);
    public static SettingsSection Google { get; } = new SettingsSection(
        "google",
        SettingsLocalizationKeys.Connection.Google,
        100,
        Connection);
    public static SettingsSection OpenRouter { get; } = new SettingsSection(
        "openrouter",
        SettingsLocalizationKeys.Connection.OpenRouter,
        110,
        Connection);
    public static SettingsSection Models { get; } = new SettingsSection(
        "models",
        SettingsLocalizationKeys.Sections.Models,
        150);
    public static SettingsSection Appearance { get; } = new SettingsSection(
        "appearance",
        SettingsLocalizationKeys.Sections.Appearance,
        200);
    public static SettingsSection StorageAndPerformance { get; } = new SettingsSection(
        "storage-and-performance",
        SettingsLocalizationKeys.Sections.StorageAndPerformance,
        300);
}
