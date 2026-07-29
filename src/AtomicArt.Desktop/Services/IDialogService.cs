namespace AtomicArt.Desktop.Services;

public interface IDialogService
{
    void ShowError(string message);

    Task ShowErrorAsync(string message, CancellationToken ct);
}
