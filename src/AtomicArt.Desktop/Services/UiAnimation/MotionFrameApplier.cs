using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal static class MotionFrameApplier
{
    public static void Apply(Control control, MotionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(frame);

        AnimatedTransformState transformState = AnimatedTransformState.GetOrCreate(control);
        transformState.Scale.ScaleX = frame.Scale;
        transformState.Scale.ScaleY = frame.Scale;
        transformState.Rotate.Angle = frame.Rotate;
        transformState.Translate.X = frame.X;
        transformState.Translate.Y = frame.Y;
        control.Opacity = frame.Opacity;
    }
}
