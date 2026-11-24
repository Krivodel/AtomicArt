using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services;

public sealed class PromptTextSizeController : TextSizeController, IPromptTextSizeController
{
    public PromptTextSizeController(ISettingsDefinitionCatalog catalog,
        IPromptTextSizeService service,
        ISettingsStateService state,
        IDoubleSettingValueConverter converter)
        : base((catalog ?? throw new ArgumentNullException(nameof(catalog))).GetRequired<PromptTextSizeSettingDefinition>(), service, state, converter)
    {
    }
}
