using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Notifications;

namespace AtomicArt.Desktop.Services;

public sealed class GenerationNotificationsSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "interface.generationNotifications";

    public string Key => KeyValue;
    public int Order => 260;
    public string DisplayNameKey => NotificationLocalizationKeys.Label;
    public SettingsSection Section => SettingsSections.Appearance;
    public bool DefaultValue => GenerationNotificationSettings.DefaultEnabled;
}
