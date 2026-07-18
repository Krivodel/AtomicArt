using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class ApiBaseAddressSettingViewModelFactory : ISettingsItemViewModelFactory
{
    private readonly IApiEndpointService _apiEndpointService;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;

    public ApiBaseAddressSettingViewModelFactory(
        IApiEndpointService apiEndpointService,
        IUiThreadDispatcher uiThreadDispatcher,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler)
    {
        _apiEndpointService = apiEndpointService
            ?? throw new ArgumentNullException(nameof(apiEndpointService));
        _uiThreadDispatcher = uiThreadDispatcher
            ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    public bool CanCreate(ISettingsDefinition definition)
    {
        return definition is ApiBaseAddressSettingDefinition;
    }

    public ISettingItemViewModel Create(ISettingsDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition is not ApiBaseAddressSettingDefinition apiBaseAddressSetting)
        {
            throw new InvalidOperationException("API base address setting definition expected.");
        }

        return new ApiBaseAddressSettingViewModel(
            apiBaseAddressSetting,
            _apiEndpointService,
            _uiThreadDispatcher,
            _settingsStateService,
            _errorHandler);
    }
}
