using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

internal sealed class SuccessfulFileRevealService : IFileRevealService
{
    public string? RevealedModelId { get; private set; }
    public string? RevealedPath { get; private set; }
    public int CallCount { get; private set; }

    public Task RevealAsync(string? path, string modelId, CancellationToken ct)
    {
        CallCount++;
        RevealedPath = path;
        RevealedModelId = modelId;

        return Task.CompletedTask;
    }
}
