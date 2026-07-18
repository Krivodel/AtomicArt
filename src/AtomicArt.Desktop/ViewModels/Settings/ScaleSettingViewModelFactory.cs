using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class ScaleSettingViewModelFactory :
    SettingItemViewModelFactory<IScaleSettingDefinition>
{
    private readonly ISettingsDefinitionCatalog _settingsDefinitionCatalog;
    private readonly IUiScaleService _uiScaleService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IUiScaleSettingValueConverter _valueConverter;
    private readonly IViewModelErrorHandler _errorHandler;

    public ScaleSettingViewModelFactory(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IUiScaleService uiScaleService,
        ISettingsStateService settingsStateService,
        IUiScaleSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler)
        : base("Scale setting definition expected.")
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);
        ArgumentNullException.ThrowIfNull(uiScaleService);
        ArgumentNullException.ThrowIfNull(settingsStateService);
        ArgumentNullException.ThrowIfNull(valueConverter);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _settingsDefinitionCatalog = settingsDefinitionCatalog;
        _uiScaleService = uiScaleService;
        _settingsStateService = settingsStateService;
        _valueConverter = valueConverter;
        _errorHandler = errorHandler;
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        IScaleSettingDefinition definition)
    {
        IReadOnlyList<UiScaleOption> scaleOptions = _settingsDefinitionCatalog.GetScaleOptions();

        return new ScaleSettingViewModel(
            definition,
            scaleOptions,
            UiScaleOptionMatcher.FindByValueOrFirst(scaleOptions, _uiScaleService.CurrentScale),
            _settingsStateService,
            _valueConverter,
            _errorHandler);
    }
}
