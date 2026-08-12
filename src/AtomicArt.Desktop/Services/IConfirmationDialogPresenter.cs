namespace AtomicArt.Desktop.Services;

public interface IConfirmationDialogPresenter
{
    bool IsOpen { get; }

    Task<bool> ShowAsync(
        ConfirmationDialogPresentation presentation,
        CancellationToken ct);

    void Update(ConfirmationDialogPresentation presentation);

    void Dismiss();
}
