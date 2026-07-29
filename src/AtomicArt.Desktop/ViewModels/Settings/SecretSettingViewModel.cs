using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class SecretSettingViewModel : SettingItemViewModel
{
    public string SecretName { get; }
    public string Placeholder => TextProvider.Get(_definition.PlaceholderKey);

    protected override IRelayCommand OperationCommand => SaveCommand;

    private readonly ISecretSettingDefinition _definition;
    private readonly ISecretStore _secretStore;
    private bool _hasPendingValue;
    private bool _isLoaded;
    private bool _isSynchronizingValue;

    [ObservableProperty]
    private string _value = string.Empty;

    public SecretSettingViewModel(
        ISecretSettingDefinition definition,
        ISecretStore secretStore,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base(definition, errorHandler, textProvider)
    {
        ArgumentNullException.ThrowIfNull(secretStore);

        _definition = definition;
        SecretName = definition.SecretName;
        _secretStore = secretStore;
    }

    public override void RefreshLocalization()
    {
        base.RefreshLocalization();
        OnPropertyChanged(nameof(Placeholder));
    }

    protected override void NotifyOperationCanExecuteChanged()
    {
        base.NotifyOperationCanExecuteChanged();
        LoadCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync(CancellationToken ct)
    {
        await RunOperationAsync(
            async () =>
            {
                string? storedValue = await _secretStore.GetSecretAsync(SecretName, ct);
                _isSynchronizingValue = true;

                try
                {
                    Value = storedValue ?? string.Empty;
                    _hasPendingValue = false;
                    _isLoaded = true;
                }
                finally
                {
                    _isSynchronizingValue = false;
                }

                NotifyOperationCanExecuteChanged();
            },
            ct,
            nameof(LoadAsync));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        await RunOperationAsync(
            async () =>
            {
                await _secretStore.SetSecretAsync(
                    SecretName,
                    Value,
                    ct);
                _hasPendingValue = false;
                _isLoaded = true;
                NotifyOperationCanExecuteChanged();
            },
            ct,
            nameof(SaveAsync));
    }

    private bool CanLoad()
    {
        return !IsLoading && !_isLoaded;
    }

    private bool CanSave()
    {
        return !IsLoading && _hasPendingValue;
    }

    partial void OnValueChanged(string value)
    {
        if (_isSynchronizingValue)
        {
            return;
        }

        _hasPendingValue = true;
        ErrorMessage = null;
        NotifyOperationCanExecuteChanged();
    }
}
