using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

internal sealed class BlockingImageViewerService : IImageViewerService
{
    public int OpenCallCount => _openCallCount;

    private readonly SemaphoreSlim _openCallSignal = new(0);
    private readonly TaskCompletionSource _releaseSignal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _openCallCount;

    public async Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        Interlocked.Increment(ref _openCallCount);
        _openCallSignal.Release();
        await _releaseSignal.Task.WaitAsync(ct).ConfigureAwait(false);
    }

    public async Task WaitForOpenCallAsync(CancellationToken ct)
    {
        await _openCallSignal.WaitAsync(ct).ConfigureAwait(false);
    }

    public void Release()
    {
        _releaseSignal.TrySetResult();
    }
}
