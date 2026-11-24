using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class PromptTextSizeSettingDefinition : TextSizeSettingDefinition, IPromptTextSizeSettingDefinition
{
    public const string KeyValue = "prompt.text-size";

    public override string Key => KeyValue;
    public override int Order => 225;
    public override string DisplayNameKey => SettingsLocalizationKeys.Appearance.PromptTextSizeLabel;
}
