using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal static class AvaloniaBitmapDecoder
{
    internal static Bitmap DecodeFile(string fullPath, CancellationToken ct)
    {
        return Decode(
            () =>
            {
                using FileStream stream = File.OpenRead(fullPath);

                return new Bitmap(stream);
            },
            ct);
    }

    internal static Bitmap Decode(Func<Bitmap> decode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Bitmap bitmap = decode();

        if (ct.IsCancellationRequested)
        {
            bitmap.Dispose();
            ct.ThrowIfCancellationRequested();
        }

        return bitmap;
    }
}
