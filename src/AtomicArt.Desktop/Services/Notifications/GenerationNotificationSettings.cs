using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services.Notifications;

public sealed class GenerationNotificationSettings : IBooleanSettingValueSource
{
    public const bool DefaultEnabled = true;

    public bool Enabled => _enabled;
    public bool CurrentValue => Enabled;

    public event EventHandler? ValueChanged;

    private volatile bool _enabled = DefaultEnabled;

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
        {
            return;
        }

        _enabled = enabled;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
