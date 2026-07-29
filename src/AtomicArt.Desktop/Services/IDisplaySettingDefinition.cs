namespace AtomicArt.Desktop.Services;

public interface IDisplaySettingDefinition : ISettingsDefinition
{
    string DisplayNameKey { get; }
    SettingsSection Section { get; }
}
