namespace AtomicArt.Desktop.Services;

public interface IFilePickerService
{
    Task<IReadOnlyList<ImageAttachmentInput>> PickImagesAsync(
        int maxInputBytes,
        CancellationToken ct);
}
