namespace AtomicArt.Desktop.Services.Settings;

public sealed class PromptTextSizeSettingsStateApplicator : ISettingsStateApplicator
{
    public string SettingKey { get; }

    private readonly IPromptTextSizeSettingDefinition _definition;
    private readonly IPromptTextSizeService _promptTextSizeService;
    private readonly IDoubleSettingValueConverter _valueConverter;

    public PromptTextSizeSettingsStateApplicator(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IPromptTextSizeService promptTextSizeService,
        IDoubleSettingValueConverter valueConverter)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        _definition = settingsDefinitionCatalog.GetRequired<PromptTextSizeSettingDefinition>();
        _promptTextSizeService = promptTextSizeService
            ?? throw new ArgumentNullException(nameof(promptTextSizeService));
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

        _promptTextSizeService.SetTextSize(textSize);
    }
}
