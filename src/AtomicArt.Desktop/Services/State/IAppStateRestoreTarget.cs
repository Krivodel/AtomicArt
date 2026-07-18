using AtomicArt.Desktop.Services.Gallery.State;

namespace AtomicArt.Desktop.Services.State;

public interface IAppStateRestoreTarget
{
    Task RestoreGenerationPanelsAsync(CancellationToken ct);

    Task RestoreGalleryAsync(IReadOnlyList<GalleryItemState> items, CancellationToken ct);
}
