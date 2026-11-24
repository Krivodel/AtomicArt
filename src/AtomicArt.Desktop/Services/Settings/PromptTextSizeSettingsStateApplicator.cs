namespace AtomicArt.Desktop.Services.Settings;

public sealed class PromptTextSizeSettingsStateApplicator : TextSizeSettingsStateApplicator<PromptTextSizeSettingDefinition>
{
    public PromptTextSizeSettingsStateApplicator(
        ISettingsDefinitionCatalog catalog, IPromptTextSizeService service, IDoubleSettingValueConverter converter)
        : base(catalog, service, converter)
    {
    }
}
