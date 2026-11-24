using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Views.Generation;

internal sealed class GenerationChatReveal : Decorator
{
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }
    public TimeSpan ResizeDuration
    {
        get => GetValue(ResizeDurationProperty);
        set => SetValue(ResizeDurationProperty, value);
    }
    public bool ContentVisible
    {
        get => GetValue(ContentVisibleProperty);
        set => SetValue(ContentVisibleProperty, value);
    }
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
    public bool IsResizeAnimationEnabled
    {
        get => GetValue(IsResizeAnimationEnabledProperty);
        set => SetValue(IsResizeAnimationEnabledProperty, value);
    }

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<GenerationChatReveal, double>(nameof(Progress));
    public static readonly StyledProperty<TimeSpan> ResizeDurationProperty =
        AvaloniaProperty.Register<GenerationChatReveal, TimeSpan>(nameof(ResizeDuration));
    public static readonly StyledProperty<bool> ContentVisibleProperty =
        AvaloniaProperty.Register<GenerationChatReveal, bool>(nameof(ContentVisible), defaultValue: true);
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<GenerationChatReveal, Orientation>(nameof(Orientation), defaultValue: Orientation.Vertical);
    public static readonly StyledProperty<bool> IsResizeAnimationEnabledProperty =
        AvaloniaProperty.Register<GenerationChatReveal, bool>(nameof(IsResizeAnimationEnabled), defaultValue: true);

    private AvaloniaUiFrameScheduler? _frames;
    private UiAnimationScheduler? _animation;
    private Size? _targetSize;
    private Size _presentedSize;

    static GenerationChatReveal()
    {
        AffectsMeasure<GenerationChatReveal>(ProgressProperty, ContentVisibleProperty, OrientationProperty, IsResizeAnimationEnabledProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Size content = base.MeasureOverride(availableSize);

        if (!ContentVisible)
        {
            content = content.WithHeight(0d);
        }

        if (_targetSize != content || !IsResizeAnimationEnabled)
        {
            _animation?.Cancel(this);
            bool animate = IsResizeAnimationEnabled && _targetSize is not null && ResizeDuration > TimeSpan.Zero
                && TopLevel.GetTopLevel(this) is not null;
            _targetSize = content;

            if (animate && TopLevel.GetTopLevel(this) is { } topLevel)
            {
                if (this.FindAncestorOfType<GenerationChatMessageView>() is { } message)
                {
                    this.FindAncestorOfType<GenerationChatView>()?.TrackMessageResize(message, ResizeDuration);
                }

                _frames ??= new AvaloniaUiFrameScheduler(topLevel);
                _animation ??= new UiAnimationScheduler(_frames);
                Size from = _presentedSize;
                _ = _animation.AnimateValueAsync(this, 0d, 1d, (int)ResizeDuration.TotalMilliseconds, 0,
                    MotionEasing.EaseRail, progress =>
                    {
                        _presentedSize = new Size(from.Width + ((content.Width - from.Width) * progress),
                            from.Height + ((content.Height - from.Height) * progress));
                        InvalidateMeasure();
                    });
            }
            else
            {
                _presentedSize = content;
            }
        }

        return Orientation == Orientation.Horizontal
            ? _presentedSize.WithWidth(_presentedSize.Width * Progress)
            : _presentedSize.WithHeight(_presentedSize.Height * Progress);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            Size content = Orientation == Orientation.Horizontal
                ? finalSize.WithWidth(child.DesiredSize.Width)
                : finalSize.WithHeight(child.DesiredSize.Height);
            child.Arrange(new Rect(content));
        }

        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animation?.Cancel(this);
        _animation = null;
        _frames?.Dispose();
        _frames = null;
        _targetSize = null;
        base.OnDetachedFromVisualTree(e);
    }
}
