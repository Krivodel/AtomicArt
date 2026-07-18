namespace AtomicArt.Desktop.Services.GalleryAnimation;

internal static class AnimationTiming
{
    public static double ClampSpeed(double value)
    {
        return Math.Clamp(value, 0.5d, 2d);
    }

    public static int ClampDelay(double value)
    {
        return Math.Clamp((int)Math.Round(value), 0, 1000);
    }

    public static int ScaleTime(int milliseconds, double speed)
    {
        if (milliseconds <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Round(milliseconds / ClampSpeed(speed)));
    }
}
