namespace AtomicArt.Desktop.Services.Settings;

public sealed class ConfirmDeletionSettingsStateApplicator : ISettingsStateApplicator
{
    public string SettingKey { get; }

    private readonly IDeletionConfirmationService _deletionConfirmationService;
    private readonly IBooleanSettingValueConverter _valueConverter;

    public ConfirmDeletionSettingsStateApplicator(
        ISettingsDefinitionCatalog settingsDefinitionCatalog,
        IDeletionConfirmationService deletionConfirmationService,
        IBooleanSettingValueConverter valueConverter)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        _deletionConfirmationService = deletionConfirmationService
            ?? throw new ArgumentNullException(nameof(deletionConfirmationService));
        _valueConverter = valueConverter
            ?? throw new ArgumentNullException(nameof(valueConverter));
        SettingKey = settingsDefinitionCatalog
            .GetRequired<ConfirmDeletionSettingDefinition>()
            .Key;
    }

    public void Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!_valueConverter.TryParse(value, out bool isConfirmationRequired))
        {
            return;
        }

        _deletionConfirmationService.SetConfirmationRequired(isConfirmationRequired);
    }
}
