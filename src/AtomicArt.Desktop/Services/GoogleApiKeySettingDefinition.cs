using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class GoogleApiKeySettingDefinition : ISecretSettingDefinition
{
    public const string KeyValue = "generation.google.apiKey";
    public const string SecretNameValue = "GoogleApiKey";

    public string Key => KeyValue;
    public int Order => 100;
    public string SecretName => SecretNameValue;
    public string DisplayName => UiStrings.SettingsGoogleApiKeyLabel;
    public string Placeholder => UiStrings.SettingsGoogleApiKeyPlaceholder;
    public string SaveButtonText => UiStrings.SettingsSave;
}
