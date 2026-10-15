using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.ViewModels.Settings;

public sealed class ImageViewerWindowLimitSettingViewModelFactory :
    SettingItemViewModelFactory<ImageViewerWindowLimitSettingDefinition>
{
    private readonly ImageViewerWindowLimitService _limitService;
    private readonly ISettingsStateService _settingsStateService;
    private readonly IViewModelErrorHandler _errorHandler;
    private readonly ILocalizationTextProvider _textProvider;

    public ImageViewerWindowLimitSettingViewModelFactory(
        ImageViewerWindowLimitService limitService,
        ISettingsStateService settingsStateService,
        IViewModelErrorHandler errorHandler,
        ILocalizationTextProvider textProvider)
        : base("Image viewer window limit setting definition expected.")
    {
        _limitService = limitService ?? throw new ArgumentNullException(nameof(limitService));
        _settingsStateService = settingsStateService ?? throw new ArgumentNullException(nameof(settingsStateService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
    }

    protected override ISettingItemViewModel CreateItemViewModel(ImageViewerWindowLimitSettingDefinition definition)
    {
        return new ImageViewerWindowLimitSettingViewModel(
            definition,
            _limitService,
            _settingsStateService,
            _errorHandler,
            _textProvider);
    }
}
