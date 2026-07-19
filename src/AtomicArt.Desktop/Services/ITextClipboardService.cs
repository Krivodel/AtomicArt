namespace AtomicArt.Desktop.Services;

public interface ITextClipboardService
{
    Task SetTextAsync(string text, CancellationToken ct);
}
