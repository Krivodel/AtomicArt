using Avalonia;

namespace Pica.Viewer.Services;

internal sealed class AspectRatioWindowResizeSession : IWindowResizeSession
{
    private const int SizingDirectionLockThreshold = 3;

    private readonly WindowRectangle _initialRectangle;
    private readonly PixelPoint _initialPointerPosition;
    private readonly WindowSizingEdges _sizingEdges;
    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private readonly double _aspectRatio;
    private WindowSizingBasis? _cornerSizingBasis;

    public AspectRatioWindowResizeSession(
        WindowRectangle initialRectangle,
        PixelPoint initialPointerPosition,
        WindowSizingEdges sizingEdges,
        int frameWidth,
        int frameHeight,
        double aspectRatio)
    {
        _initialRectangle = initialRectangle;
        _initialPointerPosition = initialPointerPosition;
        _sizingEdges = sizingEdges;
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
            _sizingEdges,
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

        if (_sizingEdges.HasFlag(WindowSizingEdges.Left))
        {
            rectangle.Left += horizontalDelta;
        }
        else if (_sizingEdges.HasFlag(WindowSizingEdges.Right))
        {
            rectangle.Right += horizontalDelta;
        }

        if (_sizingEdges.HasFlag(WindowSizingEdges.Top))
        {
            rectangle.Top += verticalDelta;
        }
        else if (_sizingEdges.HasFlag(WindowSizingEdges.Bottom))
        {
            rectangle.Bottom += verticalDelta;
        }

        return rectangle;
    }

    private WindowSizingBasis GetSizingBasis(int horizontalDelta, int verticalDelta)
    {
        bool includesHorizontalEdge = _sizingEdges.IncludesHorizontalEdge();
        bool includesVerticalEdge = _sizingEdges.IncludesVerticalEdge();

        if (includesHorizontalEdge && !includesVerticalEdge)
        {
            return WindowSizingBasis.Width;
        }

        if (includesVerticalEdge && !includesHorizontalEdge)
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
