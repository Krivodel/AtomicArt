namespace AtomicArt.Desktop.Services;

public interface IPlatformClipboardImageReader
{
    Task<ImageAttachmentInput?> TryGetImageAsync(
        int maxInputBytes,
        CancellationToken ct);
}
