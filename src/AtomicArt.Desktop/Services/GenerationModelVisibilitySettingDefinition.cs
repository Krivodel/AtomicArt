using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class GenerationModelVisibilitySettingDefinition : IDisplaySettingDefinition
{
    public const string KeyValue = "generation.visibleModels";
    public const string DefaultModelId = "nano-banana-2";

    public static IReadOnlyList<string> SupportedModelIds { get; } =
    [
        "nano-banana-2",
        "nano-banana-2-lite",
        "nano-banana-pro",
        "openrouter-nano-banana-2",
        "openrouter-nano-banana-2-lite",
        "openrouter-nano-banana-pro",
        "openrouter-gpt-image-2",
        "openrouter-gpt-image-2-5-sunburst",
        "openrouter-gpt-image-2-5-flare"
    ];

    public string Key => KeyValue;
    public int Order => 120;
    public string DisplayNameKey => SettingsLocalizationKeys.GenerationModels.Label;
    public SettingsSection Section => SettingsSections.Connection;
}
