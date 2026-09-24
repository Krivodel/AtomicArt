using Avalonia.Media.Imaging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class ImageAttachmentInput : IDisposable
{
    public string FileName { get; }

    private readonly Func<CancellationToken, Task<AttachedImageDto?>>? _read;
    private readonly Bitmap? _borrowedBitmap;
    private IDisposable? _ownedResource;
    private int _readStarted;

    internal ImageAttachmentInput(
        string fileName,
        Func<CancellationToken, Task<AttachedImageDto?>> read,
        IDisposable? ownedResource = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(read);

        FileName = fileName;
        _read = read;
        _ownedResource = ownedResource;
    }

    private ImageAttachmentInput(string fileName, Bitmap borrowedBitmap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(borrowedBitmap);

        FileName = fileName;
        _borrowedBitmap = borrowedBitmap;
    }

    public static ImageAttachmentInput FromImage(AttachedImageDto image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return new ImageAttachmentInput(
            image.FileName,
            ct =>
            {
                ct.ThrowIfCancellationRequested();

                return Task.FromResult<AttachedImageDto?>(image);
            });
    }

    internal static ImageAttachmentInput FromBorrowedBitmap(
        string fileName,
        Bitmap bitmap)
    {
        return new ImageAttachmentInput(fileName, bitmap);
    }

    internal static ImageAttachmentInput FromError(
        string fileName,
        Exception error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(error);

        return new ImageAttachmentInput(
            fileName,
            ct =>
            {
                ct.ThrowIfCancellationRequested();

                return Task.FromException<AttachedImageDto?>(error);
            });
    }

    public async Task<AttachedImageDto?> ReadAsync(CancellationToken ct)
    {
        if (_read is null)
        {
            throw new InvalidOperationException(
                $"Attached image input '{FileName}' contains a bitmap and must be prepared directly.");
        }

        EnsureReadStarts();

        return await _read(ct).ConfigureAwait(false);
    }

    public async Task<AttachedImageDto?> PrepareAsync(
        IAttachedImagePreparationService preparationService,
        ImageModelOption selectedModel,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(preparationService);
        ArgumentNullException.ThrowIfNull(selectedModel);

        if (_borrowedBitmap is Bitmap bitmap)
        {
            EnsureReadStarts();

            return await preparationService.PrepareBitmapAsync(
                FileName,
                bitmap,
                selectedModel,
                ct).ConfigureAwait(false);
        }

        AttachedImageDto? image = await ReadAsync(ct).ConfigureAwait(false);

        return image is null
            ? null
            : await preparationService.PrepareAsync(image, selectedModel, ct)
                .ConfigureAwait(false);
    }

    public void Dispose()
    {
        IDisposable? ownedResource = Interlocked.Exchange(ref _ownedResource, null);
        ownedResource?.Dispose();
    }

    private void EnsureReadStarts()
    {
        if (Interlocked.Exchange(ref _readStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                $"Attached image input '{FileName}' can only be read once.");
        }
    }
}
