namespace AtomicArt.Desktop.Services.Windowing;

internal static class LayoutDimension
{
    public static double? Normalize(double? value)
    {
        return value is double dimension && double.IsFinite(dimension) && dimension > 0d
            ? dimension
            : null;
    }
}
