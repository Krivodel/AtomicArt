using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class ApiBaseAddressSettingViewModelFactory :
    SettingItemViewModelFactory<ApiBaseAddressSettingDefinition>
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
        : base("API base address setting definition expected.")
    {
        _apiEndpointService = apiEndpointService
            ?? throw new ArgumentNullException(nameof(apiEndpointService));
        _uiThreadDispatcher = uiThreadDispatcher
            ?? throw new ArgumentNullException(nameof(uiThreadDispatcher));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        ApiBaseAddressSettingDefinition definition)
    {
        return new ApiBaseAddressSettingViewModel(
            definition,
            _apiEndpointService,
            _uiThreadDispatcher,
            _settingsStateService,
            _errorHandler);
    }
}
