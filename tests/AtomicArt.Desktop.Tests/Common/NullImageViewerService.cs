using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Tests;

internal sealed class NullImageViewerService : IImageViewerService
{
    public Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
