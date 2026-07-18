using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class BlockingAttachedImagePreparationService :
    IAttachedImagePreparationService
{
    private readonly TaskCompletionSource _started = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<AttachedImageDto?> PrepareAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(selectedModel);

        _started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);

        return image;
    }

    public async Task WaitUntilStartedAsync()
    {
        await _started.Task;
    }
}
