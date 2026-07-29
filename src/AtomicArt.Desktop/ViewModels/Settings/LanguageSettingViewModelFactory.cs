using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class LanguageSettingViewModelFactory :
    SettingItemViewModelFactory<LanguageSettingDefinition>
{
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public LanguageSettingViewModelFactory(
        ILocalizationService localizationService,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Language setting definition expected.")
    {
        _localizationService = localizationService
            ?? throw new ArgumentNullException(nameof(localizationService));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        LanguageSettingDefinition definition)
    {
        return new LanguageSettingViewModel(
            definition,
            _localizationService,
            _settingsStateService,
            _errorHandler,
            _textProvider);
    }
}
