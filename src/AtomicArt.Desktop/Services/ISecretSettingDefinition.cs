namespace AtomicArt.Desktop.Services;

public interface ISecretSettingDefinition : ISettingsDefinition
{
    string SecretName { get; }
    string DisplayName { get; }
    string Placeholder { get; }
    string SaveButtonText { get; }
}
