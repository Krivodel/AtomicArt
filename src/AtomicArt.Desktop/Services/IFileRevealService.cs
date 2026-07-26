using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services;

public interface IFileRevealService
{
    Task RevealAsync(
        string? path,
        string modelId,
        FileRevealWindowMode windowMode,
        CancellationToken ct);
}
