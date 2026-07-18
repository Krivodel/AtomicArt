namespace AtomicArt.Desktop.Services;

public interface IFileRevealService
{
    Task RevealAsync(string? path, string modelId, CancellationToken ct);
}
