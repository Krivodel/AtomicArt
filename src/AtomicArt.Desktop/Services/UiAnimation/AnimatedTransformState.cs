using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class AnimatedTransformState
{
    public ScaleTransform Scale { get; }
    public RotateTransform Rotate { get; }
    public TranslateTransform Translate { get; }

    private static readonly AttachedProperty<AnimatedTransformState?> StateProperty =
        AvaloniaProperty.RegisterAttached<AnimatedTransformState, Control, AnimatedTransformState?>("State");

    private AnimatedTransformState(Control control)
    {
        Scale = new ScaleTransform();
        Rotate = new RotateTransform();
        Translate = new TranslateTransform();

        TransformGroup transformGroup = new();
        transformGroup.Children.Add(Scale);
        transformGroup.Children.Add(Rotate);
        transformGroup.Children.Add(Translate);

        control.RenderTransformOrigin = RelativePoint.Center;
        control.RenderTransform = transformGroup;
    }

    public static AnimatedTransformState GetOrCreate(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        AnimatedTransformState? state = control.GetValue(StateProperty);
        if (state is not null)
        {
            return state;
        }

        state = new AnimatedTransformState(control);
        control.SetValue(StateProperty, state);

        return state;
    }
}
