using Avalonia.Media.Imaging;
using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.Dlss5;

internal sealed class Dlss5DisplayImage : IDlss5DisplayImage
{
    public object Value => _image;

    private readonly Bitmap _image;
    private readonly object _sync = new();
    private int _leaseCount;
    private bool _isOwnerReleased;
    private bool _isImageDisposed;

    public Dlss5DisplayImage(Bitmap image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public IPicaImageBitmapLease AcquirePicaLease()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_isOwnerReleased, this);
            _leaseCount++;
        }

        return new PicaBitmapLease(
            _image,
            ReleasePicaLease);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isOwnerReleased)
            {
                return;
            }

            _isOwnerReleased = true;
            DisposeImageIfUnused();
        }
    }

    private void ReleasePicaLease()
    {
        lock (_sync)
        {
            _leaseCount--;
            DisposeImageIfUnused();
        }
    }

    private void DisposeImageIfUnused()
    {
        if ((!_isOwnerReleased) || (_leaseCount != 0) || (_isImageDisposed))
        {
            return;
        }

        _isImageDisposed = true;
        _image.Dispose();
    }

    private sealed class PicaBitmapLease : IPicaImageBitmapLease
    {
        public Bitmap Bitmap { get; }

        private Action? _release;

        internal PicaBitmapLease(
            Bitmap bitmap,
            Action release)
        {
            Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _release, null)?.Invoke();
        }
    }
}
