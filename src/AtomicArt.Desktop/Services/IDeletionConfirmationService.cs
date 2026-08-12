namespace AtomicArt.Desktop.Services;

public interface IDeletionConfirmationService
{
    bool IsConfirmationRequired { get; }

    event EventHandler? ConfirmationRequirementChanged;

    void SetConfirmationRequired(bool isConfirmationRequired);
}
