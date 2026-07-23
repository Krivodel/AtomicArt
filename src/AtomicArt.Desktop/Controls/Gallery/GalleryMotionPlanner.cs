using Avalonia;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal static class GalleryMotionPlanner
{
    public static List<MotionFrame> BuildExistingFrames(
        Rect first,
        Rect last,
        int row,
        int column,
        Rect overlayBounds)
    {
        Point start = first.Center;
        Point end = last.Center;
        double vx = end.X - start.X;
        double vy = end.Y - start.Y;
        double distance = Math.Max(1d, Distance(vx, vy));
        double nx = vx / distance;
        double ny = vy / distance;
        double baseRotate = Math.Clamp(vx / Math.Max(42d, last.Width), -2.4d, 2.4d);
        bool wrapMove = (Math.Abs(vx) > (last.Width * 0.72d))
            && (Math.Abs(vy) > (last.Height * 0.28d));

        if (wrapMove)
        {
            return BuildWrapFrames(first, last, overlayBounds, start, end, nx, ny, vx, baseRotate);
        }

        return BuildDirectFrames(first, last, start, end, nx, ny, vx, vy, distance, baseRotate, row, column);
    }

    public static List<MotionFrame> BuildFlightFramesFromCurrent(
        int index,
        int total,
        Rect rect,
        Point startCenter,
        double startScale,
        double startOpacity,
        Point packCenter)
    {
        Point target = rect.Center;
        double middle = (total - 1) / 2.0d;
        double sign = index - middle;
        double rowTightness = total <= 2 ? 13d : total <= 4 ? 21d : 27d;
        double fanX = sign * Math.Min(Math.Min(42d, rect.Width * 0.20d), rowTightness);
        double fanY = (-Math.Abs(sign) * 8d) + ((index % 2) * 5d);
        double rotate = sign * 3.1d;
        Point fan = new(packCenter.X + fanX, packCenter.Y + fanY);
        Point midFlight = new(
            startCenter.X + ((fan.X - startCenter.X) * 0.62d),
            startCenter.Y + ((fan.Y - startCenter.Y) * 0.62d));
        Point nearTarget = new(
            startCenter.X + ((target.X - startCenter.X) * 0.92d),
            startCenter.Y + ((target.Y - startCenter.Y) * 0.92d));

        return
        [
            FromPoint(startCenter, target, startScale, rotate * 0.18d, startOpacity),
            FromPoint(midFlight, target, Math.Max(startScale, 0.74d), rotate * 1.05d, 1d),
            FromPoint(nearTarget, target, 1.006d, rotate * 0.04d, 1d),
            MotionFrame.Identity
        ];
    }

    public static Point GetPackCenter(IReadOnlyList<Rect> targetRects)
    {
        ArgumentNullException.ThrowIfNull(targetRects);

        if (targetRects.Count == 0)
        {
            return new Point();
        }

        Rect first = targetRects[0];
        double firstTop = first.Top;
        List<Rect> firstRow = targetRects
            .Where(rect => Math.Abs(rect.Top - firstTop) < 10d)
            .Take(targetRects.Count)
            .ToList();

        return new Point(
            firstRow.Average(rect => rect.Left + (rect.Width / 2d)),
            firstRow.Average(rect => rect.Top + (rect.Height * 0.42d)));
    }

    private static List<MotionFrame> BuildWrapFrames(
        Rect first,
        Rect last,
        Rect overlayBounds,
        Point start,
        Point end,
        double nx,
        double ny,
        double vx,
        double baseRotate)
    {
        double pad = (last.Width * 0.56d) + 6d;
        double bridgeX = vx < 0d
            ? Math.Min(overlayBounds.Right - pad, start.X + (last.Width * 0.36d))
            : Math.Max(overlayBounds.Left + pad, start.X - (last.Width * 0.36d));
        Point p1 = new(bridgeX, start.Y);
        Point p2 = new(bridgeX, end.Y - (ny * 8d));
        Point p3 = new(end.X + (nx * 9d), end.Y + (ny * 9d));
        Point p4 = new(end.X - (nx * 2d), end.Y - (ny * 2d));

        return
        [
            ToFrame(start, last, 1d, 0d, 1d),
            ToFrame(p1, last, 0.985d, 0d, 1d),
            ToFrame(p2, last, 0.988d, 0d, 1d),
            ToFrame(p3, last, 1.01d, baseRotate * 0.22d, 1d),
            ToFrame(p4, last, 0.998d, 0d, 1d),
            MotionFrame.Identity
        ];
    }

    private static List<MotionFrame> BuildDirectFrames(
        Rect first,
        Rect last,
        Point start,
        Point end,
        double nx,
        double ny,
        double vx,
        double vy,
        double distance,
        double baseRotate,
        int row,
        int column)
    {
        double px = -ny;
        double py = nx;
        double bend = (((row + column) % 2 == 1) ? -1d : 1d) * Math.Min(7d, distance * 0.05d);
        Point q1 = new(start.X - (nx * 5d), start.Y - (ny * 5d));
        Point q2 = new(start.X + (vx * 0.68d) + (px * bend), start.Y + (vy * 0.68d) + (py * bend));
        Point q3 = new(end.X + (nx * 8d), end.Y + (ny * 8d));

        return
        [
            ToFrame(start, last, 1d, 0d, 1d),
            ToFrame(q1, last, 0.984d, 0d, 1d),
            ToFrame(q2, last, 0.992d, baseRotate * 0.20d, 1d),
            ToFrame(q3, last, 1.008d, baseRotate * 0.12d, 1d),
            MotionFrame.Identity
        ];
    }

    private static MotionFrame FromPoint(Point point, Point target, double scale, double rotation, double opacity)
    {
        return new MotionFrame(point.X - target.X, point.Y - target.Y, scale, rotation, opacity);
    }

    private static MotionFrame ToFrame(Point point, Rect lastRect, double scale, double rotate, double opacity)
    {
        Point end = lastRect.Center;
        return new MotionFrame(point.X - end.X, point.Y - end.Y, scale, rotate, opacity);
    }

    private static double Distance(double x, double y)
    {
        return Math.Sqrt((x * x) + (y * y));
    }
}
