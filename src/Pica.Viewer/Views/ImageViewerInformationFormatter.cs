using Avalonia;

namespace Pica.Viewer.Views;

internal static class ImageViewerInformationFormatter
{
    internal static string Format(string fileName, PixelSize pixelSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if ((pixelSize.Width <= 0) || (pixelSize.Height <= 0))
        {
            return fileName;
        }

        return $"{fileName} · {pixelSize.Width} × {pixelSize.Height}";
    }
}
