using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class ApiBaseAddressSettingDefinition : ISettingsDefinition
{
    public const string KeyValue = "api.baseAddress";

    public string Key => KeyValue;
    public int Order => 50;
    public string DisplayName => UiStrings.SettingsApiBaseAddressLabel;
    public string Placeholder => UiStrings.SettingsApiBaseAddressPlaceholder;
    public string SaveButtonText => UiStrings.SettingsSaveApiBaseAddress;
}
