namespace AtomicArt.Desktop.Services;

public interface IProviderCredentialSettingDefinition : ISecretSettingDefinition
{
    string ProviderId { get; }
    string MissingCredentialMessageKey { get; }
}
