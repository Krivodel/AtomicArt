using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class SaveReferencesSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "gallery.saveReferences";
    public const bool DefaultValue = true;

    public string Key => KeyValue;
    public int Order => 260;
    public string DisplayNameKey => SettingsLocalizationKeys.SaveReferences.Label;
    public SettingsSection Section => SettingsSections.StorageAndPerformance;
}
