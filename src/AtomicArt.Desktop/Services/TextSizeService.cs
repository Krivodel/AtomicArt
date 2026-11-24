using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services;

public abstract class TextSizeService : ITextSizeService
{
    public double CurrentTextSize { get; private set; }

    public event EventHandler? TextSizeChanged;

    private readonly ITextSizeSettingDefinition _definition;

    protected TextSizeService(ITextSizeSettingDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CurrentTextSize = _definition.DefaultValue;
    }

    public void SetTextSize(double textSize)
    {
        if (!NumericSettingOptionMatcher.ContainsValue(_definition.Options, textSize))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textSize),
                textSize,
                "Text size must match a registered option.");
        }

        if (CurrentTextSize.Equals(textSize))
        {
            return;
        }

        CurrentTextSize = textSize;
        TextSizeChanged?.Invoke(this, EventArgs.Empty);
    }
}
