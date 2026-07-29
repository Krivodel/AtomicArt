using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class GpuResourceCacheSettingViewModelFactory :
    SettingItemViewModelFactory<GpuResourceCacheSettingDefinition>
{
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly ILocalizationTextProvider _textProvider;

    public GpuResourceCacheSettingViewModelFactory(
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        IAtomicArtDataPathProvider pathProvider,
        ILocalizationTextProvider textProvider)
        : base("GPU resource cache setting definition expected.")
    {
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        GpuResourceCacheSettingDefinition definition)
    {
        string? savedValue =
            GpuResourceCacheStartupSettingsReader.LoadSavedValueOrDefault(_pathProvider);
        GpuResourceCacheOptionDefinition selectedDefinition =
            GpuResourceCacheSettingOptions.FindByValueOrDefault(savedValue);
        IReadOnlyList<GpuResourceCacheOptionViewModel> options =
            GpuResourceCacheSettingOptions.Options
                .Select(option => new GpuResourceCacheOptionViewModel(
                    option,
                    _textProvider))
                .ToList();
        GpuResourceCacheOptionViewModel selectedOption = options.Single(option =>
            option.Definition == selectedDefinition);

        return new GpuResourceCacheSettingViewModel(
            definition,
            options,
            selectedOption,
            _settingsStateService,
            _errorHandler,
            _textProvider);
    }
}
