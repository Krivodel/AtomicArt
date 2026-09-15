using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.Dlss5;

public interface IDlss5DisplayImage : IDisposable
{
    object Value { get; }

    IPicaImageBitmapLease AcquirePicaLease();
}
