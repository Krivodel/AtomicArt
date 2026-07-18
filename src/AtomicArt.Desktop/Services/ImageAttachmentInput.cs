using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class ImageAttachmentInput : IDisposable
{
    public string FileName { get; }

    private readonly Func<CancellationToken, Task<AttachedImageDto?>> _read;
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

    public async Task<AttachedImageDto?> ReadAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _readStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                $"Attached image input '{FileName}' can only be read once.");
        }

        return await _read(ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        IDisposable? ownedResource = Interlocked.Exchange(ref _ownedResource, null);
        ownedResource?.Dispose();
    }
}
