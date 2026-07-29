using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class SecretSettingViewModelFactory :
    SettingItemViewModelFactory<ISecretSettingDefinition>
{
    private readonly ISecretStore _secretStore;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public SecretSettingViewModelFactory(
        ISecretStore secretStore,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Secret setting definition expected.")
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(errorHandler);
        ArgumentNullException.ThrowIfNull(textProvider);

        _secretStore = secretStore;
        _errorHandler = errorHandler;
        _textProvider = textProvider;
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        ISecretSettingDefinition definition)
    {
        return new SecretSettingViewModel(
            definition,
            _secretStore,
            _errorHandler,
            _textProvider);
    }
}
