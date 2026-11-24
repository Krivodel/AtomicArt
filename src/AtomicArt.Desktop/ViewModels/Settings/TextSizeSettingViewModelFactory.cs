using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public abstract class TextSizeSettingViewModelFactory<TDefinition> :
    SettingItemViewModelFactory<TDefinition>
    where TDefinition : class, ITextSizeSettingDefinition
{
    private readonly ITextSizeService _textSizeService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IDoubleSettingValueConverter _valueConverter;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    protected TextSizeSettingViewModelFactory(
        ITextSizeService textSizeService,
        ISettingsStateService settingsStateService,
        IDoubleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Text size setting definition expected.")
    {
        _textSizeService = textSizeService
            ?? throw new ArgumentNullException(nameof(textSizeService));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        TDefinition definition)
    {
        return new NumericSettingViewModel(
            definition,
            definition.Options,
            new TextSizeNumericSettingValueSource(_textSizeService),
            _settingsStateService,
            _valueConverter,
            _errorHandler,
            _textProvider);
    }
}
