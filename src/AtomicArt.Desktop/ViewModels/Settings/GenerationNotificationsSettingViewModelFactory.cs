using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Notifications;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class GenerationNotificationsSettingViewModelFactory(
    ISettingsStateService state,
    GenerationNotificationSettings settings,
    IBooleanSettingValueConverter converter,
    INotificationSoundStore soundStore,
    ISoundFilePickerService picker,
    IViewModelErrorHandler errorHandler,
    ILocalizationTextProvider textProvider)
    : SettingItemViewModelFactory<GenerationNotificationsSettingDefinition>("Notification setting definition expected.")
{
    protected override ISettingItemViewModel CreateItemViewModel(GenerationNotificationsSettingDefinition definition)
    {
        return new GenerationNotificationsSettingViewModel(definition, settings, state, converter,
            soundStore, picker, errorHandler, textProvider);
    }
}
