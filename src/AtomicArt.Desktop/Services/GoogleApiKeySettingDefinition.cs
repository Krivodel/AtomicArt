using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class GoogleApiKeySettingDefinition : ISecretSettingDefinition
{
    public const string KeyValue = "generation.google.apiKey";
    public const string SecretNameValue = "GoogleApiKey";

    public string Key => KeyValue;
    public int Order => 100;
    public string SecretName => SecretNameValue;
    public string DisplayNameKey => SettingsLocalizationKeys.GoogleApiKey.Label;
    public SettingsSection Section => SettingsSections.Connection;
    public string PlaceholderKey => SettingsLocalizationKeys.GoogleApiKey.Label;
}
