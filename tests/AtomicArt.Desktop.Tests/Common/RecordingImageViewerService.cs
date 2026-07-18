using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests;

internal sealed class RecordingImageViewerService : IImageViewerService
{
    public GalleryImageViewerRequest? LastRequest { get; private set; }
    public int OpenCallCount { get; private set; }

    public Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        LastRequest = request;
        OpenCallCount++;

        return Task.CompletedTask;
    }
}
