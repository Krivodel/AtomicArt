using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed record ExistingAnimation(
    List<MotionFrame> Frames,
    int Duration,
    int Delay,
    Func<double, double> Ease);
