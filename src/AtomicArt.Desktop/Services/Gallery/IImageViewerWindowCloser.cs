namespace AtomicArt.Desktop.Services.Gallery;

public interface IImageViewerWindowCloser
{
    Task CloseAllAsync(CancellationToken ct);
}
