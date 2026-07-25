namespace AtomicArt.Desktop.Services.Gallery;

public interface IDataRootViewerPreparationService
{
    Task CloseAllAsync(CancellationToken ct);
}
