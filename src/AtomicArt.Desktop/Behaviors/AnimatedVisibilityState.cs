using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Behaviors;

internal sealed class AnimatedVisibilityState
{
    private const double HiddenOffsetY = 24d;

    private readonly Control _control;
    private readonly IUiFrameScheduler? _frameScheduler;
    private AvaloniaUiFrameScheduler? _frames;
    private UiAnimationScheduler? _animation;
    private int _version;

    internal AnimatedVisibilityState(Control control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _frameScheduler = control.GetValue(AnimatedVisibilityBehavior.FrameSchedulerProperty);
        control.AttachedToVisualTree += OnAttached;
        control.DetachedFromVisualTree += OnDetached;
    }

    internal void Update()
    {
        int version = ++_version;
        _animation?.Cancel(_control);
        bool shown = AnimatedVisibilityBehavior.GetIsShown(_control);
        TimeSpan duration = AnimatedVisibilityBehavior.GetDuration(_control);
        _control.IsHitTestVisible = shown;
        MotionFrame target = new(0d, shown ? 0d : HiddenOffsetY, 1d, 0d, shown ? 1d : 0d);

        if (!_control.IsAttachedToVisualTree() || duration <= TimeSpan.Zero
            || TopLevel.GetTopLevel(_control) is not { } topLevel)
        {
            MotionFrameApplier.Apply(_control, target);
            _control.IsVisible = shown;

            return;
        }

        AnimatedTransformState transform = AnimatedTransformState.GetOrCreate(_control);
        MotionFrame start = new(0d, transform.Translate.Y, 1d, 0d, _control.Opacity);
        _control.IsVisible = true;

        if (_animation is null)
        {
            _frames = _frameScheduler is null ? new AvaloniaUiFrameScheduler(topLevel) : null;
            _animation = new UiAnimationScheduler(_frameScheduler ?? _frames
                ?? throw new InvalidOperationException("The visibility animation has no frame scheduler."));
        }

        _ = _animation.AnimateAsync(_control, new MotionFrame[] { start, target },
            (int)duration.TotalMilliseconds, 0, MotionEasing.EaseRail, () =>
            {
                if (version == _version)
                {
                    _control.IsVisible = shown;
                }
            });
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        bool shown = AnimatedVisibilityBehavior.GetIsShown(_control);
        MotionFrameApplier.Apply(_control, new MotionFrame(0d, shown ? 0d : HiddenOffsetY, 1d, 0d, shown ? 1d : 0d));
        _control.IsVisible = shown;
        _control.IsHitTestVisible = shown;
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _version++;
        _animation?.Cancel(_control);
        _animation = null;
        _frames?.Dispose();
        _frames = null;
    }
}
