using AtomicArt.Desktop.Services.Notifications;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class GenerationNotificationsSettingsStateApplicator : ISettingsStateApplicator
{
    public string SettingKey => GenerationNotificationsSettingDefinition.KeyValue;

    private readonly GenerationNotificationSettings _settings;
    private readonly IBooleanSettingValueConverter _converter;

    public GenerationNotificationsSettingsStateApplicator(
        GenerationNotificationSettings settings,
        IBooleanSettingValueConverter converter)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }

    public void Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_converter.TryParse(value, out bool enabled))
        {
            _settings.SetEnabled(enabled);
        }
    }
}
