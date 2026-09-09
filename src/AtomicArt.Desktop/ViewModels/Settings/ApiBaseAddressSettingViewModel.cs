using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class ApiBaseAddressSettingViewModel : SettingItemViewModel, IDisposable
{
    public string Placeholder => TextProvider.Get(_definition.PlaceholderKey);
    public bool IsCatalogLoading => _modelCatalog.IsLoading;
    public bool IsBusy => IsLoading || IsCatalogLoading;

    protected override IRelayCommand OperationCommand => SaveCommand;

    private readonly ApiBaseAddressSettingDefinition _definition;
    private readonly IApiEndpointService _apiEndpointService;
    private readonly IImageModelOptionCatalog _modelCatalog;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly ISettingsStateService _settingsStateService;
    private readonly CancellationTokenSource _disposeCancellationSource = new();
    private string _committedValue;
    private bool _hasValidationErrorMessage;
    private bool _isDisposed;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [CustomValidation(
        typeof(ApiBaseAddressSettingViewModel),
        nameof(ValidateBaseAddress))]
    private string _value;

    public ApiBaseAddressSettingViewModel(
        ApiBaseAddressSettingDefinition definition,
        IApiEndpointService apiEndpointService,
        IImageModelOptionCatalog modelCatalog,
        IUiThreadDispatcher uiThreadDispatcher,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        ArgumentNullException.ThrowIfNull(apiEndpointService);
        ArgumentNullException.ThrowIfNull(modelCatalog);
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(settingsStateService);

        _definition = definition;
        _apiEndpointService = apiEndpointService;
        _modelCatalog = modelCatalog;
        _uiThreadDispatcher = uiThreadDispatcher;
        _settingsStateService = settingsStateService;
        _value = apiEndpointService.BaseAddress.ToString();
        _committedValue = _value;
        _apiEndpointService.BaseAddressChanged += OnApiBaseAddressChanged;
        _modelCatalog.LoadingChanged += OnCatalogLoadingChanged;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsLoading)
            || e.PropertyName == nameof(IsCatalogLoading))
        {
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public static ValidationResult? ValidateBaseAddress(string? value, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ApiBaseAddress.TryCreate(value, out _))
        {
            return ValidationResult.Success;
        }

        if (context.ObjectInstance is not ApiBaseAddressSettingViewModel viewModel)
        {
            throw new InvalidOperationException(
                "API base address validation requires its owning view model.");
        }

        return new ValidationResult(viewModel.TextProvider.Get(
            SettingsLocalizationKeys.ApiBaseAddress.Invalid));
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        OnPropertyChanged(nameof(Placeholder));
        ValidateAllProperties();

        if (_hasValidationErrorMessage)
        {
            ErrorMessage = TextProvider.Get(
                SettingsLocalizationKeys.ApiBaseAddress.Invalid);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _apiEndpointService.BaseAddressChanged -= OnApiBaseAddressChanged;
        _modelCatalog.LoadingChanged -= OnCatalogLoadingChanged;
        _disposeCancellationSource.Cancel();
        _disposeCancellationSource.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        ValidateAllProperties();

        if (HasErrors
            || !ApiBaseAddress.TryCreate(Value, out ApiBaseAddress? baseAddress)
            || baseAddress is null)
        {
            _hasValidationErrorMessage = true;
            ErrorMessage = TextProvider.Get(
                SettingsLocalizationKeys.ApiBaseAddress.Invalid);
            return;
        }

        await RunOperationAsync(
            async () =>
            {
                string normalizedValue = baseAddress.ToString();
                _settingsStateService.ApplyValue(_definition, normalizedValue);
                await _settingsStateService.SaveValueAsync(_definition, normalizedValue, ct);
                Value = normalizedValue;
                _committedValue = normalizedValue;
            },
            ct,
            nameof(SaveAsync));
    }

    private bool CanSave()
    {
        return !IsBusy
            && !string.Equals(Value, _committedValue, StringComparison.Ordinal);
    }

    private async Task SynchronizeValueAsync()
    {
        await ViewModelUiDispatch.RunAsync(
            _uiThreadDispatcher,
            SynchronizeValue,
            _disposeCancellationSource.Token,
            ErrorHandler,
            nameof(SynchronizeValueAsync));
    }

    private void SynchronizeValue()
    {
        string value = _apiEndpointService.BaseAddress.ToString();
        _committedValue = value;
        Value = value;
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnValueChanged(string value)
    {
        _hasValidationErrorMessage = false;
        ErrorMessage = null;
    }

    private void OnApiBaseAddressChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _ = SynchronizeValueAsync();
    }

    private void OnCatalogLoadingChanged(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        OnPropertyChanged(nameof(IsCatalogLoading));
        NotifyOperationCanExecuteChanged();
    }
}
