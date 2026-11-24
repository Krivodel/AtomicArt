namespace AtomicArt.Desktop.Services.Settings;

public abstract class TextSizeSettingsStateApplicator<TDefinition> : ISettingsStateApplicator
    where TDefinition : class, ITextSizeSettingDefinition
{
    public string SettingKey { get; }

    private readonly ITextSizeSettingDefinition _definition;
    private readonly ITextSizeService _textSizeService;
    private readonly IDoubleSettingValueConverter _valueConverter;

    protected TextSizeSettingsStateApplicator(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        ITextSizeService textSizeService,
        IDoubleSettingValueConverter valueConverter)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        _definition = settingsDefinitionCatalog.GetRequired<TDefinition>();
        _textSizeService = textSizeService
            ?? throw new ArgumentNullException(nameof(textSizeService));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        SettingKey = _definition.Key;
    }

    public void Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!_valueConverter.TryParse(value, out double textSize))
        {
            return;
        }

        if (!NumericSettingOptionMatcher.ContainsValue(_definition.Options, textSize))
        {
            return;
        }

        _textSizeService.SetTextSize(textSize);
    }
}
