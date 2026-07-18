using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SecretSettingViewModelFactory : ISettingsItemViewModelFactory
{
    private readonly ISecretStore _secretStore;
    private readonly IViewModelErrorHandler _errorHandler;

    public SecretSettingViewModelFactory(
        ISecretStore secretStore,
        IViewModelErrorHandler errorHandler)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _secretStore = secretStore;
        _errorHandler = errorHandler;
    }

    public bool CanCreate(ISettingsDefinition definition)
    {
        return definition is ISecretSettingDefinition;
    }

    public ISettingItemViewModel Create(ISettingsDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition is not ISecretSettingDefinition secretSetting)
        {
            throw new InvalidOperationException("Secret setting definition expected.");
        }

        return new SecretSettingViewModel(
            secretSetting,
            _secretStore,
            _errorHandler);
    }
}
