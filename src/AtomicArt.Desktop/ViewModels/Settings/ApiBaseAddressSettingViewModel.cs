using System.ComponentModel.DataAnnotations;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Settings;
using AtomicArt.Desktop.ViewModels;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class ApiBaseAddressSettingViewModel : SettingItemViewModel, IDisposable
{
    public string Placeholder { get; }

    protected override IRelayCommand OperationCommand => SaveCommand;

    private readonly ApiBaseAddressSettingDefinition _definition;
    private readonly IApiEndpointService _apiEndpointService;
    private readonly IUiThreadDispatcher _uiThreadDispatcher;
    private readonly ISettingsStateService _settingsStateService;
    private readonly CancellationTokenSource _disposeCancellationSource = new();
    private string _committedValue;
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
        IUiThreadDispatcher uiThreadDispatcher,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler)
        : base(definition, errorHandler)
    {
        ArgumentNullException.ThrowIfNull(apiEndpointService);
        ArgumentNullException.ThrowIfNull(uiThreadDispatcher);
        ArgumentNullException.ThrowIfNull(settingsStateService);

        _definition = definition;
        _apiEndpointService = apiEndpointService;
        _uiThreadDispatcher = uiThreadDispatcher;
        _settingsStateService = settingsStateService;
        _value = apiEndpointService.BaseAddress.ToString();
        _committedValue = _value;
        Placeholder = definition.Placeholder;
        _apiEndpointService.BaseAddressChanged += OnApiBaseAddressChanged;
    }

    public static ValidationResult? ValidateBaseAddress(string? value, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ApiBaseAddress.TryCreate(value, out _)
            ? ValidationResult.Success
            : new ValidationResult(UiStrings.SettingsApiBaseAddressInvalid);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _apiEndpointService.BaseAddressChanged -= OnApiBaseAddressChanged;
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
            ErrorMessage = UiStrings.SettingsApiBaseAddressInvalid;
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
        return !IsLoading
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
}
