using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class UiScaleSettingDefinition : IScaleSettingDefinition
{
    public const string KeyValue = "ui.scale";

    public string Key => KeyValue;
    public int Order => 200;
    public string DisplayName => UiStrings.SettingsScaleLabel;
    public string ApplyButtonText => UiStrings.SettingsApplyScale;
}
