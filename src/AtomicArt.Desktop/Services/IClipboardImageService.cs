namespace AtomicArt.Desktop.Services;

public interface IClipboardImageService
{
    Task<ImageAttachmentInput?> TryGetImageAsync(int maxInputBytes, CancellationToken ct);
}
