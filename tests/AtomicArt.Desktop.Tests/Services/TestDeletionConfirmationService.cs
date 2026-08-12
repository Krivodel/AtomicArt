using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

internal sealed class TestDeletionConfirmationService : IDeletionConfirmationService
{
    public bool IsConfirmationRequired { get; private set; }

    public event EventHandler? ConfirmationRequirementChanged;

    public TestDeletionConfirmationService(bool isConfirmationRequired = true)
    {
        IsConfirmationRequired = isConfirmationRequired;
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
