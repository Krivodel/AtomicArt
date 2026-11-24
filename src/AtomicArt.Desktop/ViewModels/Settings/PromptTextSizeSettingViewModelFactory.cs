using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class PromptTextSizeSettingViewModelFactory : TextSizeSettingViewModelFactory<IPromptTextSizeSettingDefinition>
{
    public PromptTextSizeSettingViewModelFactory(
        IPromptTextSizeService service,
        ISettingsStateService state,
        IDoubleSettingValueConverter converter,
        IViewModelErrorHandler errors,
        ILocalizationTextProvider textProvider)
        : base(service, state, converter, errors, textProvider)
    {
    }
}
