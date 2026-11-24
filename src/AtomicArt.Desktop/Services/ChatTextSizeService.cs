namespace AtomicArt.Desktop.Services;

public sealed class ChatTextSizeService : TextSizeService, IChatTextSizeService
{
    public ChatTextSizeService(ISettingsDefinitionCatalog catalog)
        : base((catalog ?? throw new ArgumentNullException(nameof(catalog))).GetRequired<ChatTextSizeSettingDefinition>())
    {
    }
}
