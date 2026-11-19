using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation.LocalCodex;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class LocalCodexSettingViewModelFactory(
    ISettingsStateService state,
    LocalCodexSettings settings,
    LocalCodexAppServer appServer,
    IViewModelErrorHandler errorHandler,
    ILocalizationTextProvider textProvider)
    : SettingItemViewModelFactory<LocalCodexSettingDefinition>("Local Codex setting definition expected.")
{
    protected override ISettingItemViewModel CreateItemViewModel(LocalCodexSettingDefinition definition)
    {
        return new LocalCodexSettingViewModel(definition, state, settings, appServer, errorHandler, textProvider);
    }
}
