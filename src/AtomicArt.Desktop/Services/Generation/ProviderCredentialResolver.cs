using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class ProviderCredentialResolver : IGenerationModelService
{
    private readonly ISecretStore _secretStore;
    private readonly IReadOnlyDictionary<string, IProviderCredentialSettingDefinition> _settingsByProvider;

    public ProviderCredentialResolver(
        ISecretStore secretStore,
        ISettingsDefinitionCatalog settingsDefinitionCatalog)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _settingsByProvider = settingsDefinitionCatalog.GetSettings()
            .OfType<IProviderCredentialSettingDefinition>()
            .ToDictionary(setting => setting.ProviderId, StringComparer.Ordinal);
    }

    public bool RequiresCredential(ImageModelOption model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return !string.Equals(model.Provider, GenerationProviderIds.Test, StringComparison.Ordinal);
    }

    public async Task<ProviderCredentialResolution> ResolveAsync(
        ImageModelOption model,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!RequiresCredential(model))
        {
            return ProviderCredentialResolution.NoCredentialRequired;
        }

        if (!_settingsByProvider.TryGetValue(model.Provider, out IProviderCredentialSettingDefinition? setting))
        {
            throw new InvalidOperationException(
                $"No credential setting is registered for provider '{model.Provider}'.");
        }

        string? credential = await _secretStore
            .GetSecretAsync(setting.SecretName, ct)
            .ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(credential)
            ? new ProviderCredentialResolution(null, setting.MissingCredentialMessageKey)
            : new ProviderCredentialResolution(credential.Trim(), null);
    }
}

public sealed record ProviderCredentialResolution(
    string? Credential,
    string? MissingCredentialMessageKey)
{
    public static ProviderCredentialResolution NoCredentialRequired { get; } = new(string.Empty, null);
}
