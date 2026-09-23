namespace AtomicArt.Desktop.Services;

public interface IClipboardImageService
{
    Task SetImageAsync(string imagePath, CancellationToken ct);
    Task<ImageAttachmentInput?> TryGetImageAsync(int maxInputBytes, CancellationToken ct);
}
