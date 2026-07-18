using Avalonia;

namespace AtomicArt.Desktop.Views.Gallery;

internal static class GenerationPreviewExpansionCalculator
{
    private const double ExpandedPreviewScale = 1.7d;

    public static (Size Size, Vector Translation) Calculate(
        Size previewSize,
        Size sourceSize,
        Rect previewBounds,
        Rect viewportBounds)
    {
        EnsurePositiveSize(previewSize, nameof(previewSize));
        EnsurePositiveSize(sourceSize, nameof(sourceSize));
        EnsurePositiveSize(viewportBounds.Size, nameof(viewportBounds));

        Size expandedSize = CalculateExpandedSize(previewSize, sourceSize);
        double centeredLeft = previewBounds.Center.X - (expandedSize.Width / 2d);
        double centeredTop = previewBounds.Center.Y - (expandedSize.Height / 2d);
        double fittedLeft = FitCoordinate(
            centeredLeft,
            expandedSize.Width,
            viewportBounds.Left,
            viewportBounds.Right);
        double fittedTop = FitCoordinate(
            centeredTop,
            expandedSize.Height,
            viewportBounds.Top,
            viewportBounds.Bottom);
        Vector translation = new(
            fittedLeft - previewBounds.Left,
            fittedTop - previewBounds.Top);

        return (expandedSize, translation);
    }

    private static Size CalculateExpandedSize(Size previewSize, Size sourceSize)
    {
        double shortSide = Math.Min(previewSize.Width, previewSize.Height);
        double aspectRatio = sourceSize.Width / sourceSize.Height;
        Size aspectRatioSize;

        if (aspectRatio >= 1d)
        {
            aspectRatioSize = new Size(shortSide * aspectRatio, shortSide);
        }
        else
        {
            aspectRatioSize = new Size(shortSide, shortSide / aspectRatio);
        }

        return new Size(
            aspectRatioSize.Width * ExpandedPreviewScale,
            aspectRatioSize.Height * ExpandedPreviewScale);
    }

    private static double FitCoordinate(
        double coordinate,
        double contentLength,
        double viewportStart,
        double viewportEnd)
    {
        double viewportLength = viewportEnd - viewportStart;

        if (contentLength >= viewportLength)
        {
            return viewportStart;
        }

        return Math.Clamp(coordinate, viewportStart, viewportEnd - contentLength);
    }

    private static void EnsurePositiveSize(Size size, string parameterName)
    {
        if ((size.Width <= 0d) || (size.Height <= 0d))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                size,
                "Preview expansion dimensions must be positive.");
        }
    }
}
