using AtomicArt.Desktop.Services.Localization;

namespace AtomicArt.Desktop.Services.Settings;

public sealed class LocalizationSettingsStateApplicator : ISettingsStateApplicator
{
    public string SettingKey { get; }

    private readonly ILocalizationService _localizationService;

    public LocalizationSettingsStateApplicator(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        _localizationService = localizationService
            ?? throw new ArgumentNullException(nameof(localizationService));
        SettingKey = settingsDefinitionCatalog
            .GetRequired<LanguageSettingDefinition>()
            .Key;
    }

    public void Apply(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        _localizationService.SelectSavedOrEnglishFallback(value);
    }
}
