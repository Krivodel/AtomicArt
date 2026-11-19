using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class LocalCodexSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "generation.localCodex";

    public string Key => KeyValue;
    public int Order => 200;
    public string DisplayNameKey => SettingsLocalizationKeys.LocalCodex.Title;
    public SettingsSection Section => SettingsSections.Models;
}
