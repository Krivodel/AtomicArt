namespace AtomicArt.Desktop.Services.Gallery.Deletion;

public interface IGalleryItemDeletionService
{
    Task DeleteFilesAsync(GalleryItemDeletionRequest request, CancellationToken ct);
    Task DeleteFilesAsync(
        IReadOnlyList<GalleryItemDeletionRequest> requests,
        CancellationToken ct);
}
