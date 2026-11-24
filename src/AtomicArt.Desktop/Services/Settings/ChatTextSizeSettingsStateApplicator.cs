namespace AtomicArt.Desktop.Services.Settings;

public sealed class ChatTextSizeSettingsStateApplicator : TextSizeSettingsStateApplicator<ChatTextSizeSettingDefinition>
{
    public ChatTextSizeSettingsStateApplicator(
        ISettingsDefinitionCatalog catalog, IChatTextSizeService service, IDoubleSettingValueConverter converter)
        : base(catalog, service, converter)
    {
    }
}
