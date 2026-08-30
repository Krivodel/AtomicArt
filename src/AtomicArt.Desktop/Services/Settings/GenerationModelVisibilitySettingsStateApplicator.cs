using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class GenerationModelVisibilitySettingsStateApplicator : ISettingsStateApplicator
{
    public string SettingKey => GenerationModelVisibilitySettingDefinition.KeyValue;

    private readonly GenerationModelVisibilityService _visibilityService;

    public GenerationModelVisibilitySettingsStateApplicator(
        GenerationModelVisibilityService visibilityService)
    {
        _visibilityService = visibilityService
            ?? throw new ArgumentNullException(nameof(visibilityService));
    }

    public void Apply(string value)
    {
        _visibilityService.ApplySerializedValue(value);
    }
}
