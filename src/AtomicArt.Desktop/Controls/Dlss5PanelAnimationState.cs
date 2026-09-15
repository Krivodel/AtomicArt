namespace AtomicArt.Desktop.Controls;

internal readonly record struct Dlss5PanelAnimationState(
    double FirstCardProgress,
    double ResultCardProgress,
    double SourceOpacity,
    double ResultOpacity,
    double PlaceholderOpacity,
    double ResultScale,
    double ResultRotation);
