namespace AtomicArt.Desktop.Services;

public interface IDialogService
{
    bool ShowError(string message);

    Task<bool> ShowErrorAsync(string message, CancellationToken ct);
}
