namespace AtomicArt.Desktop.Services;

public sealed class PromptTextSizeService : TextSizeService, IPromptTextSizeService
{
    public PromptTextSizeService(ISettingsDefinitionCatalog catalog)
        : base((catalog ?? throw new ArgumentNullException(nameof(catalog))).GetRequired<PromptTextSizeSettingDefinition>())
    {
    }
}
