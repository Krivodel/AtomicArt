using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class ApiBaseAddressSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "api.baseAddress";

    public string Key => KeyValue;
    public int Order => 50;
    public string DisplayNameKey => SettingsLocalizationKeys.ApiBaseAddress.Label;
    public SettingsSection Section => SettingsSections.Connection;
    public string PlaceholderKey => SettingsLocalizationKeys.ApiBaseAddress.Placeholder;
}
