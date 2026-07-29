using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class PromptTextSizeSettingViewModelFactory :
    SettingItemViewModelFactory<IPromptTextSizeSettingDefinition>
{
    private readonly IPromptTextSizeService _promptTextSizeService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IDoubleSettingValueConverter _valueConverter;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public PromptTextSizeSettingViewModelFactory(
        IPromptTextSizeService promptTextSizeService,
        ISettingsStateService settingsStateService,
        IDoubleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Prompt text size setting definition expected.")
    {
        _promptTextSizeService = promptTextSizeService
            ?? throw new ArgumentNullException(nameof(promptTextSizeService));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        IPromptTextSizeSettingDefinition definition)
    {
        return new NumericSettingViewModel(
            definition,
            definition.Options,
            new PromptTextSizeNumericSettingValueSource(_promptTextSizeService),
            _settingsStateService,
            _valueConverter,
            _errorHandler,
            _textProvider);
    }
}
