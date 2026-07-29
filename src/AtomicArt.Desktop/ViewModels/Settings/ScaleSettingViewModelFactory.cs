using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class ScaleSettingViewModelFactory :
    SettingItemViewModelFactory<IScaleSettingDefinition>
{
    private readonly ISettingsDefinitionCatalog _settingsDefinitionCatalog;
    private readonly IUiScaleService _uiScaleService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IDoubleSettingValueConverter _valueConverter;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public ScaleSettingViewModelFactory(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IUiScaleService uiScaleService,
        ISettingsStateService settingsStateService,
        IDoubleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Scale setting definition expected.")
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);
        ArgumentNullException.ThrowIfNull(uiScaleService);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(textProvider);

        _settingsDefinitionCatalog = settingsDefinitionCatalog;
        _uiScaleService = uiScaleService;
        _settingsStateService = settingsStateService;
        _valueConverter = valueConverter;
        _errorHandler = errorHandler;
        _textProvider = textProvider;
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        IScaleSettingDefinition definition)
    {
        IReadOnlyList<UiScaleOption> scaleOptions = _settingsDefinitionCatalog.GetScaleOptions();

        return new NumericSettingViewModel(
            definition,
            scaleOptions,
            new UiScaleNumericSettingValueSource(_uiScaleService),
            _settingsStateService,
            _valueConverter,
            _errorHandler,
            _textProvider);
    }
}
