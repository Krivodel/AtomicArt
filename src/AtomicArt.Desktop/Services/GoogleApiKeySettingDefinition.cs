using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class GoogleApiKeySettingDefinition : IProviderCredentialSettingDefinition
{
    public const string KeyValue = "generation.google.apiKey";
    public const string SecretNameValue = "GoogleApiKey";

    public string Key => KeyValue;
    public int Order => 100;
    public string SecretName => SecretNameValue;
    public string DisplayNameKey => SettingsLocalizationKeys.Connection.ApiKey;
    public SettingsSection Section => SettingsSections.Google;
    public string PlaceholderKey => SettingsLocalizationKeys.GoogleApiKey.Label;
    public string ProviderId => GenerationProviderIds.Google;
    public string MissingCredentialMessageKey => GenerationUiLocalizationKeys.Errors.GoogleApiKeyMissing;
}
