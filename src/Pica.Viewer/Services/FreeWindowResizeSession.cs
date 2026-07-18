using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class FreeWindowResizeSession : IWindowResizeSession
{
    private const int MinimumWindowExtent = 64;

    private readonly WindowRectangle _initialRectangle;
    private readonly PixelPoint _initialPointerPosition;
    private readonly WindowSizingEdge _sizingEdge;

    public FreeWindowResizeSession(
        WindowRectangle initialRectangle,
        PixelPoint initialPointerPosition,
        WindowSizingEdge sizingEdge)
    {
        _initialRectangle = initialRectangle;
        _initialPointerPosition = initialPointerPosition;
        _sizingEdge = sizingEdge;
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
        if ((_sizingEdge == WindowSizingEdge.Left)
            || (_sizingEdge == WindowSizingEdge.TopLeft)
            || (_sizingEdge == WindowSizingEdge.BottomLeft))
        {
            rectangle.Left = Math.Min(
                rectangle.Left + delta,
                rectangle.Right - MinimumWindowExtent);
        }
        else if ((_sizingEdge == WindowSizingEdge.Right)
            || (_sizingEdge == WindowSizingEdge.TopRight)
            || (_sizingEdge == WindowSizingEdge.BottomRight))
        {
            rectangle.Right = Math.Max(
                rectangle.Right + delta,
                rectangle.Left + MinimumWindowExtent);
        }
    }

    private void ApplyVerticalDelta(ref WindowRectangle rectangle, int delta)
    {
        if ((_sizingEdge == WindowSizingEdge.Top)
            || (_sizingEdge == WindowSizingEdge.TopLeft)
            || (_sizingEdge == WindowSizingEdge.TopRight))
        {
            rectangle.Top = Math.Min(
                rectangle.Top + delta,
                rectangle.Bottom - MinimumWindowExtent);
        }
        else if ((_sizingEdge == WindowSizingEdge.Bottom)
            || (_sizingEdge == WindowSizingEdge.BottomLeft)
            || (_sizingEdge == WindowSizingEdge.BottomRight))
        {
            rectangle.Bottom = Math.Max(
                rectangle.Bottom + delta,
                rectangle.Top + MinimumWindowExtent);
        }
    }
}
