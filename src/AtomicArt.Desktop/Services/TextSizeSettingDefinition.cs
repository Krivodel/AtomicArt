using AtomicArt.Desktop.Models;

namespace AtomicArt.Desktop.Services;

public abstract class TextSizeSettingDefinition : ITextSizeSettingDefinition
{
    public abstract string Key { get; }
    public abstract int Order { get; }
    public abstract string DisplayNameKey { get; }
    public SettingsSection Section => SettingsSections.Appearance;
    public virtual double DefaultValue => DefaultTextSize;
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
