using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services;

public sealed class ChatTextSizeController : TextSizeController, IChatTextSizeController
{
    public ChatTextSizeController(ISettingsDefinitionCatalog catalog,
        IChatTextSizeService service,
        ISettingsStateService state,
        IDoubleSettingValueConverter converter)
        : base((catalog ?? throw new ArgumentNullException(nameof(catalog))).GetRequired<ChatTextSizeSettingDefinition>(), service, state, converter)
    {
    }
}
