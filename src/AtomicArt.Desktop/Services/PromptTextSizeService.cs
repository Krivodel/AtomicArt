using AtomicArt.Desktop.Services.Settings;

namespace AtomicArt.Desktop.Services;

public sealed class PromptTextSizeService : IPromptTextSizeService
{
    public double CurrentTextSize { get; private set; }

    public event EventHandler? TextSizeChanged;

    private readonly IPromptTextSizeSettingDefinition _definition;

    public PromptTextSizeService(ISettingsDefinitionCatalog settingsDefinitionCatalog)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        _definition = settingsDefinitionCatalog.GetRequired<PromptTextSizeSettingDefinition>();
        CurrentTextSize = _definition.DefaultValue;
    }

    public void SetTextSize(double textSize)
    {
        if (!NumericSettingOptionMatcher.ContainsValue(_definition.Options, textSize))
        {
            throw new ArgumentOutOfRangeException(
                nameof(textSize),
                textSize,
                "Prompt text size must match a registered option.");
        }

        if (CurrentTextSize.Equals(textSize))
        {
            return;
        }

        CurrentTextSize = textSize;
        TextSizeChanged?.Invoke(this, EventArgs.Empty);
    }
}
