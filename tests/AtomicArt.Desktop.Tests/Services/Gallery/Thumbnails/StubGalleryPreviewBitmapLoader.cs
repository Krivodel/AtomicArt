using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.Gallery.Thumbnails;

namespace AtomicArt.Desktop.Tests.Services.Gallery.Thumbnails;

internal sealed class StubGalleryPreviewBitmapLoader : IGalleryPreviewBitmapLoader
{
    public int InvocationCount { get; private set; }

    private readonly Func<string, CancellationToken, Task<Bitmap?>> _loadAsync;

    public StubGalleryPreviewBitmapLoader(
        Func<string, CancellationToken, Task<Bitmap?>> loadAsync)
    {
        _loadAsync = loadAsync ?? throw new ArgumentNullException(nameof(loadAsync));
    }

    public Task<Bitmap?> LoadAsync(string imagePath, CancellationToken ct)
    {
        InvocationCount++;

        return _loadAsync(imagePath, ct);
    }
}
