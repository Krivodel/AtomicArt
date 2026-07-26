using AtomicArt.Desktop.Services;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

internal sealed class ThrowingFileRevealService : IFileRevealService
{
    public Task RevealAsync(
        string? path,
        string modelId,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        throw new InvalidOperationException("Invalid path");
    }
}
