using AtomicArt.Desktop.Services.Generation.LocalCodex;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class LocalCodexSettingsStateApplicator : ISettingsStateApplicator
{
    private readonly LocalCodexSettings _settings;

    public LocalCodexSettingsStateApplicator(LocalCodexSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string SettingKey => LocalCodexSettingDefinition.KeyValue;

    public void Apply(string value)
    {
        _settings.ApplySerializedValue(value);
    }
}
