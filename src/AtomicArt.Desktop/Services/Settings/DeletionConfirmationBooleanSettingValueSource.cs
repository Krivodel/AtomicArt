namespace AtomicArt.Desktop.Services.Settings;

internal sealed class DeletionConfirmationBooleanSettingValueSource : IBooleanSettingValueSource
{
    public bool CurrentValue => _deletionConfirmationService.IsConfirmationRequired;

    public event EventHandler? ValueChanged
    {
        add => _deletionConfirmationService.ConfirmationRequirementChanged += value;
        remove => _deletionConfirmationService.ConfirmationRequirementChanged -= value;
    }

    private readonly IDeletionConfirmationService _deletionConfirmationService;

    public DeletionConfirmationBooleanSettingValueSource(
        IDeletionConfirmationService deletionConfirmationService)
    {
        _deletionConfirmationService = deletionConfirmationService
            ?? throw new ArgumentNullException(nameof(deletionConfirmationService));
    }
}
