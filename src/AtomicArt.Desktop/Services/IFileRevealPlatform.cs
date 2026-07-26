namespace AtomicArt.Desktop.Services;

public interface IFileRevealPlatform
{
    Task RevealAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct);
}
