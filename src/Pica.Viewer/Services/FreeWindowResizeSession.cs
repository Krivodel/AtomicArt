using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class FreeWindowResizeSession : IWindowResizeSession
{
    private const int MinimumWindowExtent = 64;

    private readonly WindowRectangle _initialRectangle;
    private readonly PixelPoint _initialPointerPosition;
    private readonly WindowSizingEdges _sizingEdges;

    public FreeWindowResizeSession(
        WindowRectangle initialRectangle,
        PixelPoint initialPointerPosition,
        WindowSizingEdges sizingEdges)
    {
        _initialRectangle = initialRectangle;
        _initialPointerPosition = initialPointerPosition;
        _sizingEdges = sizingEdges;
    }

    public WindowRectangle Calculate(PixelPoint pointerPosition)
    {
        int horizontalDelta = pointerPosition.X - _initialPointerPosition.X;
        int verticalDelta = pointerPosition.Y - _initialPointerPosition.Y;
        WindowRectangle rectangle = _initialRectangle;
        ApplyHorizontalDelta(ref rectangle, horizontalDelta);
        ApplyVerticalDelta(ref rectangle, verticalDelta);

        return rectangle;
    }

    private void ApplyHorizontalDelta(ref WindowRectangle rectangle, int delta)
    {
        if (_sizingEdges.HasFlag(WindowSizingEdges.Left))
        {
            rectangle.Left = Math.Min(
                rectangle.Left + delta,
                rectangle.Right - MinimumWindowExtent);
        }
        else if (_sizingEdges.HasFlag(WindowSizingEdges.Right))
        {
            rectangle.Right = Math.Max(
                rectangle.Right + delta,
                rectangle.Left + MinimumWindowExtent);
        }
    }

    private void ApplyVerticalDelta(ref WindowRectangle rectangle, int delta)
    {
        if (_sizingEdges.HasFlag(WindowSizingEdges.Top))
        {
            rectangle.Top = Math.Min(
                rectangle.Top + delta,
                rectangle.Bottom - MinimumWindowExtent);
        }
        else if (_sizingEdges.HasFlag(WindowSizingEdges.Bottom))
        {
            rectangle.Bottom = Math.Max(
                rectangle.Bottom + delta,
                rectangle.Top + MinimumWindowExtent);
        }
    }
}
