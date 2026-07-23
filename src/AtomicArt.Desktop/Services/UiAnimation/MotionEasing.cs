namespace AtomicArt.Desktop.Services.UiAnimation;

internal static class MotionEasing
{
    private const double DerivativeEpsilon = 0.0001d;
    private const int IterationCount = 5;

    public static double EaseOut(double value)
    {
        return 1d - Math.Pow(1d - value, 3d);
    }

    public static double EaseRail(double value)
    {
        return CubicBezier(value, 0.22d, 0d, 0.18d, 1d);
    }

    public static double EaseMaterial(double value)
    {
        return CubicBezier(value, 0.16d, 0.84d, 0.18d, 1d);
    }

    private static double CubicBezier(double x, double x1, double y1, double x2, double y2)
    {
        double t = x;

        for (int i = 0; i < IterationCount; i++)
        {
            double currentX = Bezier(t, 0d, x1, x2, 1d);
            double derivative = BezierDerivative(t, 0d, x1, x2, 1d);

            if (Math.Abs(derivative) < DerivativeEpsilon)
            {
                break;
            }

            t -= (currentX - x) / derivative;
            t = Math.Clamp(t, 0d, 1d);
        }

        return Bezier(t, 0d, y1, y2, 1d);
    }

    private static double Bezier(double t, double p0, double p1, double p2, double p3)
    {
        double u = 1d - t;

        return (u * u * u * p0)
            + (3d * u * u * t * p1)
            + (3d * u * t * t * p2)
            + (t * t * t * p3);
    }

    private static double BezierDerivative(double t, double p0, double p1, double p2, double p3)
    {
        double u = 1d - t;

        return (3d * u * u * (p1 - p0))
            + (6d * u * t * (p2 - p1))
            + (3d * t * t * (p3 - p2));
    }
}
