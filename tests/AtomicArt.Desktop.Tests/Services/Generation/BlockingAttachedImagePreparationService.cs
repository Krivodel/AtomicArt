using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

internal sealed class BlockingAttachedImagePreparationService :
    AttachedImagePreparationServiceTestDouble
{
    private readonly TaskCompletionSource _started = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<AttachedImageDto?> PrepareCoreAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        _started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);

        return image;
    }

    public async Task WaitUntilStartedAsync()
    {
        await _started.Task;
    }
}
