namespace AtomicArt.Desktop.Services.Dlss5;

public interface IDlss5SourceOpener
{
    Task<bool> OpenFromImagePathAsync(string imagePath, CancellationToken ct);
}
