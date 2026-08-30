using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class GenerationModelVisibilitySettingViewModelFactory :
    SettingItemViewModelFactory<GenerationModelVisibilitySettingDefinition>
{
    private readonly IGenerationModelCatalogApiClient _catalogApiClient;
    private readonly IImageModelOptionCatalog _modelCatalog;
    private readonly GenerationModelVisibilityService _visibilityService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public GenerationModelVisibilitySettingViewModelFactory(
        IGenerationModelCatalogApiClient catalogApiClient,
        IImageModelOptionCatalog modelCatalog,
        GenerationModelVisibilityService visibilityService,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Generation model visibility setting definition expected.")
    {
        _catalogApiClient = catalogApiClient ?? throw new ArgumentNullException(nameof(catalogApiClient));
        _modelCatalog = modelCatalog ?? throw new ArgumentNullException(nameof(modelCatalog));
        _visibilityService = visibilityService ?? throw new ArgumentNullException(nameof(visibilityService));
        _settingsStateService = settingsStateService ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        GenerationModelVisibilitySettingDefinition definition)
    {
        return new GenerationModelVisibilitySettingViewModel(
            definition,
            _catalogApiClient,
            _modelCatalog,
            _visibilityService,
            _settingsStateService,
            _errorHandler,
            _textProvider);
    }
}
