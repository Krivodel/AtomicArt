using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class ApiBaseAddressSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "api.baseAddress";

    public string Key => KeyValue;
    public int Order => 50;
    public string DisplayName => UiStrings.SettingsApiBaseAddressLabel;
    public SettingsSection Section => SettingsSections.Connection;
    public string Placeholder => UiStrings.SettingsApiBaseAddressPlaceholder;
}
