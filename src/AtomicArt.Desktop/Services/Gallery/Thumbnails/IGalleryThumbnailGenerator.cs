namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public interface IGalleryThumbnailGenerator
{
    Task<byte[]> CreateThumbnailAsync(string imagePath, CancellationToken ct);
}
