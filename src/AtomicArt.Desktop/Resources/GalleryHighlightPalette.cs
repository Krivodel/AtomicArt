using Avalonia;
using Avalonia.Media;

namespace AtomicArt.Desktop.Resources;

public static class GalleryHighlightPalette
{
    public const double ShadowBlurRadius = 18d;
    public const double ShadowOpacity = 0.8d;

    public static Color BackgroundColor { get; } = Color.Parse("#2434D399");
    public static Color BorderColor { get; } = Color.Parse("#F273F2A7");
    public static Color ShadowColor { get; } = Color.Parse("#22C55E");
    public static Thickness BorderThickness { get; } = new Thickness(2d);
}
