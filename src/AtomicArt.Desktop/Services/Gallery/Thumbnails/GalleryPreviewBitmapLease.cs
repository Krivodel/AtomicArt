using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class GalleryPreviewBitmapLease : IDisposable
{
    public Bitmap Bitmap { get; }

    private Action? _release;

    internal GalleryPreviewBitmapLease(Bitmap bitmap, Action release)
    {
        Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public void Dispose()
    {
        Action? release = Interlocked.Exchange(ref _release, null);
        release?.Invoke();
    }
}
