using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class ConfirmDeletionSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "gallery.confirmDeletion";

    public string Key => KeyValue;
    public int Order => 250;
    public string DisplayNameKey => SettingsLocalizationKeys.Appearance.ConfirmDeletionLabel;
    public SettingsSection Section => SettingsSections.Appearance;
    public bool DefaultValue => true;
}
