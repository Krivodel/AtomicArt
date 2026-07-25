using AtomicArt.Desktop.Models;
using AtomicArt.Desktop.Resources;

namespace AtomicArt.Desktop.Services;

public sealed class PromptTextSizeSettingDefinition : IPromptTextSizeSettingDefinition
{
    public const string KeyValue = "prompt.text-size";

    public string Key => KeyValue;
    public int Order => 225;
    public string DisplayName => UiStrings.SettingsPromptTextSizeLabel;
    public string ActionText => UiStrings.SettingsApply;
    public double DefaultValue => DefaultTextSize;
    public IReadOnlyList<NumericSettingOption> Options => TextSizeOptions;

    private const int MinimumTextSize = 8;
    private const int MaximumTextSize = 32;
    private const double DefaultTextSize = 14d;

    private static readonly IReadOnlyList<NumericSettingOption> TextSizeOptions =
        Enumerable.Range(
                MinimumTextSize,
                MaximumTextSize - MinimumTextSize + 1)
            .Select(value => new NumericSettingOption(value.ToString(), value))
            .ToList();
}
