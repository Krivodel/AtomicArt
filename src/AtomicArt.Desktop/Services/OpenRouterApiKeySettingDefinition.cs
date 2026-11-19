using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class OpenRouterApiKeySettingDefinition : IProviderCredentialSettingDefinition
{
    public const string KeyValue = "generation.openrouter.apiKey";
    public const string SecretNameValue = "OpenRouterApiKey";

    public string Key => KeyValue;
    public int Order => 110;
    public string SecretName => SecretNameValue;
    public string DisplayNameKey => SettingsLocalizationKeys.Connection.ApiKey;
    public SettingsSection Section => SettingsSections.OpenRouter;
    public string PlaceholderKey => SettingsLocalizationKeys.OpenRouterApiKey.Label;
    public string ProviderId => GenerationProviderIds.OpenRouter;
    public string MissingCredentialMessageKey => GenerationUiLocalizationKeys.Errors.OpenRouterApiKeyMissing;
}
