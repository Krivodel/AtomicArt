using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class OpenRouterManagementKeySettingDefinition : ISecretSettingDefinition
{
    public const string SecretNameValue = "OpenRouterManagementKey";

    public string Key => "openrouter.managementKey";
    public int Order => 111;
    public string SecretName => SecretNameValue;
    public string DisplayNameKey => SettingsLocalizationKeys.OpenRouterManagementKey.Label;
    public string PlaceholderKey => SettingsLocalizationKeys.OpenRouterManagementKey.Label;
    public SettingsSection Section => SettingsSections.Connection;
}
