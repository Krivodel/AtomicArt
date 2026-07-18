using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.ViewModels;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class SecretSettingViewModel : ObservableObject, ISettingItemViewModel
{
    public string Key { get; }
    public int Order { get; }
    public string SecretName { get; }
    public string DisplayName { get; }
    public string Placeholder { get; }
    public string SaveButtonText { get; }
    private readonly ISecretStore _secretStore;
    private readonly IViewModelErrorHandler _errorHandler;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isLoading;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;
    [ObservableProperty]
    private string _value = string.Empty;

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public SecretSettingViewModel(
        ISecretSettingDefinition definition,
        ISecretStore secretStore,
        IViewModelErrorHandler errorHandler)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(errorHandler);

        Key = definition.Key;
        Order = definition.Order;
        SecretName = definition.SecretName;
        DisplayName = definition.DisplayName;
        Placeholder = definition.Placeholder;
        SaveButtonText = definition.SaveButtonText;
        _secretStore = secretStore;
        _errorHandler = errorHandler;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken ct)
    {
        await ViewModelAsyncOperation.RunAsync(
            async () =>
            {
                await _secretStore.SetSecretAsync(
                    SecretName,
                    Value,
                    ct);
                Value = string.Empty;
            },
            ct,
            _errorHandler,
            nameof(SaveAsync),
            value => IsLoading = value,
            value => ErrorMessage = value);
    }

    private bool CanSave()
    {
        return !IsLoading;
    }
}
