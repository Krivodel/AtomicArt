using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SaveReferencesSettingViewModelFactory(
    SaveReferencesSetting setting,
    ISettingsStateService settingsStateService,
    IBooleanSettingValueConverter valueConverter,
    IViewModelErrorHandler errorHandler,
    ILocalizationTextProvider textProvider)
    : SettingItemViewModelFactory<SaveReferencesSettingDefinition>("Save references setting definition expected.")
{
    protected override ISettingItemViewModel CreateItemViewModel(SaveReferencesSettingDefinition definition)
    {
        return new BooleanSettingViewModel(
            definition, setting, settingsStateService, valueConverter, errorHandler, textProvider);
    }
}
