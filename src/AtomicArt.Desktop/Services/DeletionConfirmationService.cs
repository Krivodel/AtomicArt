namespace AtomicArt.Desktop.Services;

public sealed class DeletionConfirmationService : IDeletionConfirmationService
{
    public bool IsConfirmationRequired { get; private set; }

    public event EventHandler? ConfirmationRequirementChanged;

    public DeletionConfirmationService(ISettingsDefinitionCatalog settingsDefinitionCatalog)
    {
        ArgumentNullException.ThrowIfNull(settingsDefinitionCatalog);

        ConfirmDeletionSettingDefinition definition = settingsDefinitionCatalog
            .GetRequired<ConfirmDeletionSettingDefinition>();
        IsConfirmationRequired = definition.DefaultValue;
    }

    public void SetConfirmationRequired(bool isConfirmationRequired)
    {
        if (IsConfirmationRequired == isConfirmationRequired)
        {
            return;
        }

        IsConfirmationRequired = isConfirmationRequired;
        ConfirmationRequirementChanged?.Invoke(this, EventArgs.Empty);
    }
}
