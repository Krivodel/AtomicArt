using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class GpuResourceCacheSettingViewModelFactory : ISettingsItemViewModelFactory
{
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;

    public GpuResourceCacheSettingViewModelFactory(
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler)
    {
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public bool CanCreate(ISettingsDefinition definition)
    {
        return definition is GpuResourceCacheSettingDefinition;
    }

    public ISettingItemViewModel Create(ISettingsDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition is not GpuResourceCacheSettingDefinition gpuResourceCacheSetting)
        {
            throw new InvalidOperationException("GPU resource cache setting definition expected.");
        }

        string? savedValue = GpuResourceCacheStartupSettingsReader.LoadSavedValueOrDefault();
        GpuResourceCacheOption selectedOption =
            GpuResourceCacheSettingOptions.FindByValueOrDefault(savedValue);

        return new GpuResourceCacheSettingViewModel(
            gpuResourceCacheSetting,
            GpuResourceCacheSettingOptions.Options,
            selectedOption,
            _settingsStateService,
            _errorHandler);
    }
}
