namespace AtomicArt.Desktop.Services;

public sealed record SettingsSection(
    string Key,
    string DisplayNameKey,
    int Order);
