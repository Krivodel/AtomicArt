namespace AtomicArt.Desktop.Services;

public interface IDialogService
{
    void ShowError(string message);
    void ShowLocalizedError(string localizationKey);

    Task ShowErrorAsync(string message, CancellationToken ct);
    Task ShowLocalizedErrorAsync(string localizationKey, CancellationToken ct);
    Task<bool> ShowConfirmationAsync(
        LocalizedConfirmationDialogRequest request,
        CancellationToken ct);
}
