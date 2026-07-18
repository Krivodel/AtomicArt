namespace AtomicArt.Desktop.Services;

public interface ISecretSettingDefinition : IDisplaySettingDefinition
{
    string SecretName { get; }
    string Placeholder { get; }
    string SaveButtonText { get; }
}
