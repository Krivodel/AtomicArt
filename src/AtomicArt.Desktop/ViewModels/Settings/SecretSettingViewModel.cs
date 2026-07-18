using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed partial class SecretSettingViewModel : SettingItemViewModel
{
    public string SecretName { get; }
    public string Placeholder { get; }
    public string SaveButtonText { get; }

    private readonly ISecretStore _secretStore;

    [ObservableProperty]
    private string _value = string.Empty;

    public SecretSettingViewModel(
        ISecretSettingDefinition definition,
        ISecretStore secretStore,
        IViewModelErrorHandler errorHandler)
        : base(definition, errorHandler)
    {
        ArgumentNullException.ThrowIfNull(secretStore);

        SecretName = definition.SecretName;
        Placeholder = definition.Placeholder;
        SaveButtonText = definition.SaveButtonText;
        _secretStore = secretStore;
    }

    protected override void NotifyActionCanExecuteChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
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
                Value = string.Empty;
            },
            ct,
            nameof(SaveAsync));
    }

    private bool CanSave()
    {
        return !IsLoading;
    }
}
