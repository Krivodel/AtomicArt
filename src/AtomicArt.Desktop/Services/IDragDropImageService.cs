using Avalonia.Input;

namespace AtomicArt.Desktop.Services;

public interface IDragDropImageService
{
    Task<IReadOnlyList<ImageAttachmentInput>> ExtractImagesAsync(
        IDataTransfer dataTransfer,
        int maxInputBytes,
        CancellationToken ct);
}
