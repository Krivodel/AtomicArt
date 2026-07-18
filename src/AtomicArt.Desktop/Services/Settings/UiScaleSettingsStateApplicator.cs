namespace AtomicArt.Desktop.Services.Settings;

public sealed class UiScaleSettingsStateApplicator : ISettingsStateApplicator
{
    private readonly ISettingsDefinitionCatalog _settingsDefinitionCatalog;
    private readonly IUiScaleService _uiScaleService;
    private readonly IUiScaleSettingValueConverter _valueConverter;

    public string SettingKey { get; }

    public UiScaleSettingsStateApplicator(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IUiScaleService uiScaleService,
        IUiScaleSettingValueConverter valueConverter)
    {
        _settingsDefinitionCatalog = settingsDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(settingsDefinitionCatalog));
        _uiScaleService = uiScaleService ?? throw new ArgumentNullException(nameof(uiScaleService));
        _valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        SettingKey = _settingsDefinitionCatalog.GetRequired<UiScaleSettingDefinition>().Key;
    }

    public void Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!_valueConverter.TryParse(value, out double scale))
        {
            return;
        }

        if (!UiScaleOptionMatcher.ContainsValue(_settingsDefinitionCatalog.GetScaleOptions(), scale))
        {
            return;
        }

        _uiScaleService.SetScale(scale);
    }
}
