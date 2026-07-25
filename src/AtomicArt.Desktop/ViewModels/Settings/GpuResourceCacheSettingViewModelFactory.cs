using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class GpuResourceCacheSettingViewModelFactory :
    SettingItemViewModelFactory<GpuResourceCacheSettingDefinition>
{
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly IAtomicArtDataPathProvider _pathProvider;

    public GpuResourceCacheSettingViewModelFactory(
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        IAtomicArtDataPathProvider pathProvider)
        : base("GPU resource cache setting definition expected.")
    {
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        GpuResourceCacheSettingDefinition definition)
    {
        string? savedValue =
            GpuResourceCacheStartupSettingsReader.LoadSavedValueOrDefault(_pathProvider);
        GpuResourceCacheOption selectedOption =
            GpuResourceCacheSettingOptions.FindByValueOrDefault(savedValue);

        return new GpuResourceCacheSettingViewModel(
            definition,
            GpuResourceCacheSettingOptions.Options,
            selectedOption,
            _settingsStateService,
            _errorHandler);
    }
}
