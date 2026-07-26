namespace AtomicArt.Desktop.Services.Gallery.State;

public interface IGalleryStateConsistencyService
{
    Task ReconcileAsync(CancellationToken ct);
}
