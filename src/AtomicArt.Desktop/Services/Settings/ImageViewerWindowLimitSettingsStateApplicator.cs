using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class ImageViewerWindowLimitSettingsStateApplicator : ISettingsStateApplicator
{
    public string SettingKey => ImageViewerWindowLimitSettingDefinition.KeyValue;

    private readonly ImageViewerWindowLimitService _limitService;

    public ImageViewerWindowLimitSettingsStateApplicator(
        ImageViewerWindowLimitService limitService)
    {
        _limitService = limitService ?? throw new ArgumentNullException(nameof(limitService));
    }

    public void Apply(string value)
    {
        _limitService.ApplySerializedValue(value);
    }
}
