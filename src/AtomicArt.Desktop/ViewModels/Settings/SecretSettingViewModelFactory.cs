using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SecretSettingViewModelFactory :
    SettingItemViewModelFactory<ISecretSettingDefinition>
{
    private readonly ISecretStore _secretStore;
    private readonly IViewModelErrorHandler _errorHandler;

    public SecretSettingViewModelFactory(
        ISecretStore secretStore,
        IViewModelErrorHandler errorHandler)
        : base("Secret setting definition expected.")
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _secretStore = secretStore;
        _errorHandler = errorHandler;
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        ISecretSettingDefinition definition)
    {
        return new SecretSettingViewModel(
            definition,
            _secretStore,
            _errorHandler);
    }
}
