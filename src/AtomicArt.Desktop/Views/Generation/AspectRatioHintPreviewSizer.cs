using System.Globalization;

namespace AtomicArt.Desktop.Views.Generation;

internal static class AspectRatioHintPreviewSizer
{
    public static AspectRatioHintPreviewSize Calculate(
        string aspectRatio,
        double maxWidth,
        double maxHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aspectRatio);

        string[] parts = aspectRatio.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double widthRatio)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double heightRatio)
            || widthRatio <= 0d
            || heightRatio <= 0d)
        {
            return new AspectRatioHintPreviewSize(maxWidth, maxHeight);
        }

        double ratio = widthRatio / heightRatio;
        double width = maxWidth;
        double height = width / ratio;

        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * ratio;
        }

        return new AspectRatioHintPreviewSize(width, height);
    }
}
