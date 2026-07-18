using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

internal sealed class ThrowingFileRevealService : IFileRevealService
{
    public Task RevealAsync(string? path, string modelId, CancellationToken ct)
    {
        throw new InvalidOperationException("Invalid path");
    }
}
