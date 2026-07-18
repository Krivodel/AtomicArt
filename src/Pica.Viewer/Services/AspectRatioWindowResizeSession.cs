using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class AspectRatioWindowResizeSession : IWindowResizeSession
{
    private const int SizingDirectionLockThreshold = 3;

    private readonly WindowRectangle _initialRectangle;
    private readonly PixelPoint _initialPointerPosition;
    private readonly WindowSizingEdge _sizingEdge;
    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly double _aspectRatio;
    private WindowSizingBasis? _cornerSizingBasis;

    public AspectRatioWindowResizeSession(
        WindowRectangle initialRectangle,
        PixelPoint initialPointerPosition,
        WindowSizingEdge sizingEdge,
        int frameWidth,
        int frameHeight,
        double aspectRatio)
    {
        _initialRectangle = initialRectangle;
        _initialPointerPosition = initialPointerPosition;
        _sizingEdge = sizingEdge;
        _frameWidth = frameWidth;
        _frameHeight = frameHeight;
        _aspectRatio = aspectRatio;
    }

    public WindowRectangle Calculate(PixelPoint pointerPosition)
    {
        int horizontalDelta = pointerPosition.X - _initialPointerPosition.X;
        int verticalDelta = pointerPosition.Y - _initialPointerPosition.Y;
        WindowRectangle requestedRectangle = CreateRequestedRectangle(
            horizontalDelta,
            verticalDelta);
        WindowSizingBasis sizingBasis = GetSizingBasis(horizontalDelta, verticalDelta);

        return AspectRatioWindowSizing.Constrain(
            requestedRectangle,
            _sizingEdge,
            sizingBasis,
            _frameWidth,
            _frameHeight,
            _aspectRatio);
    }

    private WindowRectangle CreateRequestedRectangle(
        int horizontalDelta,
        int verticalDelta)
    {
        WindowRectangle rectangle = _initialRectangle;

        if ((_sizingEdge == WindowSizingEdge.Left)
            || (_sizingEdge == WindowSizingEdge.TopLeft)
            || (_sizingEdge == WindowSizingEdge.BottomLeft))
        {
            rectangle.Left += horizontalDelta;
        }
        else if ((_sizingEdge == WindowSizingEdge.Right)
            || (_sizingEdge == WindowSizingEdge.TopRight)
            || (_sizingEdge == WindowSizingEdge.BottomRight))
        {
            rectangle.Right += horizontalDelta;
        }

        if ((_sizingEdge == WindowSizingEdge.Top)
            || (_sizingEdge == WindowSizingEdge.TopLeft)
            || (_sizingEdge == WindowSizingEdge.TopRight))
        {
            rectangle.Top += verticalDelta;
        }
        else if ((_sizingEdge == WindowSizingEdge.Bottom)
            || (_sizingEdge == WindowSizingEdge.BottomLeft)
            || (_sizingEdge == WindowSizingEdge.BottomRight))
        {
            rectangle.Bottom += verticalDelta;
        }

        return rectangle;
    }

    private WindowSizingBasis GetSizingBasis(int horizontalDelta, int verticalDelta)
    {
        if ((_sizingEdge == WindowSizingEdge.Left) || (_sizingEdge == WindowSizingEdge.Right))
        {
            return WindowSizingBasis.Width;
        }

        if ((_sizingEdge == WindowSizingEdge.Top) || (_sizingEdge == WindowSizingEdge.Bottom))
        {
            return WindowSizingBasis.Height;
        }

        if (_cornerSizingBasis is { } lockedBasis)
        {
            return lockedBasis;
        }

        int horizontalDistance = Math.Abs(horizontalDelta);
        int verticalDistance = Math.Abs(verticalDelta);

        if (Math.Max(horizontalDistance, verticalDistance) >= SizingDirectionLockThreshold)
        {
            _cornerSizingBasis = horizontalDistance >= verticalDistance
                ? WindowSizingBasis.Width
                : WindowSizingBasis.Height;
        }

        return _cornerSizingBasis ?? WindowSizingBasis.Width;
    }
}
