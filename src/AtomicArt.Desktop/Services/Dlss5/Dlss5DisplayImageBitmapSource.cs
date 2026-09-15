using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.Dlss5;

internal sealed class Dlss5DisplayImageBitmapSource : IPicaImageBitmapSource
{
    public bool IsFileBacked { get; }

    private readonly IDlss5DisplayImage _displayImage;

    internal Dlss5DisplayImageBitmapSource(
        IDlss5DisplayImage displayImage,
        bool isFileBacked)
    {
        _displayImage = displayImage
            ?? throw new ArgumentNullException(nameof(displayImage));
        IsFileBacked = isFileBacked;
    }

    public ValueTask<IPicaImageBitmapLease> AcquireAsync(
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_displayImage.AcquirePicaLease());
    }
}
