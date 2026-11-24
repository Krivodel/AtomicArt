using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class ChatTextSizeSettingDefinition : TextSizeSettingDefinition, ITextSizeSettingDefinition
{
    public const string KeyValue = "chat.text-size";

    public override string Key => KeyValue;
    public override int Order => 226;
    public override string DisplayNameKey => SettingsLocalizationKeys.Appearance.ChatTextSizeLabel;
    public override double DefaultValue => DefaultChatTextSize;

    private const double DefaultChatTextSize = 16d;
}
