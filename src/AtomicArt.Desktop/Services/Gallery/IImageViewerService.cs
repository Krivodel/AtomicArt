namespace AtomicArt.Desktop.Services.Gallery;

public interface IImageViewerService
{
    Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct);
}
