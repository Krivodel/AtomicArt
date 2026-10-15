using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class ImageViewerWindowLimitSettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "viewer.windowLimit";
    public const int Unlimited = 0;
    public const int MinimumLimit = 1;
    public const int MaximumLimit = 100;

    public string Key => KeyValue;
    public int Order => 230;
    public string DisplayNameKey => SettingsLocalizationKeys.ImageViewer.WindowLimitLabel;
    public SettingsSection Section => SettingsSections.Appearance;
    public int DefaultValue => 3;

    public static bool IsValidLimit(decimal value)
    {
        return value >= MinimumLimit && value <= MaximumLimit && decimal.Truncate(value) == value;
    }
}
