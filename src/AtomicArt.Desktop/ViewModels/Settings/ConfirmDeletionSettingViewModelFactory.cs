using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class ConfirmDeletionSettingViewModelFactory :
    SettingItemViewModelFactory<ConfirmDeletionSettingDefinition>
{
    private readonly IDeletionConfirmationService _deletionConfirmationService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IBooleanSettingValueConverter _valueConverter;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public ConfirmDeletionSettingViewModelFactory(
        IDeletionConfirmationService deletionConfirmationService,
        ISettingsStateService settingsStateService,
        IBooleanSettingValueConverter valueConverter,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Confirm deletion setting definition expected.")
    {
        _deletionConfirmationService = deletionConfirmationService
            ?? throw new ArgumentNullException(nameof(deletionConfirmationService));
        _settingsStateService = settingsStateService
            ?? throw new ArgumentNullException(nameof(settingsStateService));
        _valueConverter = valueConverter
            ?? throw new ArgumentNullException(nameof(valueConverter));
        _errorHandler = errorHandler
            ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider
            ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(
        ConfirmDeletionSettingDefinition definition)
    {
        return new BooleanSettingViewModel(
            definition,
            new DeletionConfirmationBooleanSettingValueSource(
                _deletionConfirmationService),
            _settingsStateService,
            _valueConverter,
            _errorHandler,
            _textProvider);
    }
}
