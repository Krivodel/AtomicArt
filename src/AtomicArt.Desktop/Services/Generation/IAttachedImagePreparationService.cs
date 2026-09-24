using Avalonia.Media.Imaging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public interface IAttachedImagePreparationService
{
    Task<AttachedImageDto?> PrepareAsync(
        AttachedImageDto image,
        ImageModelOption selectedModel,
        CancellationToken ct);

    Task<AttachedImageDto?> PrepareBitmapAsync(
        string fileName,
        Bitmap bitmap,
        ImageModelOption selectedModel,
        CancellationToken ct);
}
