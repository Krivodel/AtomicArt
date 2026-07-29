using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class LanguageSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "ui.language";

    public string Key => KeyValue;
    public int Order => 150;
    public string DisplayNameKey => SettingsLocalizationKeys.Language.Label;
    public SettingsSection Section => SettingsSections.Appearance;
}
