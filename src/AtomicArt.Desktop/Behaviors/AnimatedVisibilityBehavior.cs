using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Behaviors;

public static class AnimatedVisibilityBehavior
{
    public static readonly AttachedProperty<bool> IsShownProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsShown",
            typeof(AnimatedVisibilityBehavior), defaultValue: true);
    public static readonly AttachedProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.RegisterAttached<Control, TimeSpan>("Duration", typeof(AnimatedVisibilityBehavior),
            defaultValue: TimeSpan.FromMilliseconds(DefaultDurationMilliseconds));

    internal static readonly AttachedProperty<IUiFrameScheduler?> FrameSchedulerProperty =
        AvaloniaProperty.RegisterAttached<Control, IUiFrameScheduler?>("FrameScheduler", typeof(AnimatedVisibilityBehavior));

    private const int DefaultDurationMilliseconds = 200;

    private static readonly AttachedProperty<AnimatedVisibilityState?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, AnimatedVisibilityState?>("State", typeof(AnimatedVisibilityBehavior));

    static AnimatedVisibilityBehavior()
    {
        IsShownProperty.Changed.AddClassHandler<Control>(OnIsShownChanged);
    }

    public static bool GetIsShown(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, IsShownProperty);
    }

    public static void SetIsShown(Control control, bool value)
    {
        AttachedPropertyValueAccessor.Set(control, IsShownProperty, value);
    }

    public static TimeSpan GetDuration(Control control)
    {
        return AttachedPropertyValueAccessor.Get(control, DurationProperty);
    }

    public static void SetDuration(Control control, TimeSpan value)
    {
        AttachedPropertyValueAccessor.Set(control, DurationProperty, value);
    }

    private static void OnIsShownChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        AnimatedVisibilityState? state = control.GetValue(StateProperty);

        if (state is null)
        {
            state = new AnimatedVisibilityState(control);
            control.SetValue(StateProperty, state);
        }

        state.Update();
    }
}
