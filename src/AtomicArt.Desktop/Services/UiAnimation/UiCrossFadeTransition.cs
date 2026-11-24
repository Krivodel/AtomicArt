using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

public sealed class UiCrossFadeTransition : AvaloniaObject, IPageTransition
{
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<UiCrossFadeTransition, TimeSpan>(nameof(Duration));

    private readonly Func<TopLevel, IUiFrameScheduler> _createFrames;

    public UiCrossFadeTransition()
        : this(topLevel => new AvaloniaUiFrameScheduler(topLevel))
    {
    }

    internal UiCrossFadeTransition(Func<TopLevel, IUiFrameScheduler> createFrames)
    {
        _createFrames = createFrames ?? throw new ArgumentNullException(nameof(createFrames));
    }

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Control? outgoing = from as Control;
        Control? incoming = to as Control;
        Control? target = incoming ?? outgoing;

        if (incoming is not null)
        {
            incoming.IsVisible = true;
        }

        if (target is null || TopLevel.GetTopLevel(target) is not { } topLevel)
        {
            return;
        }

        IUiFrameScheduler frames = _createFrames(topLevel);
        UiAnimationScheduler animation = new(frames);

        try
        {
            await animation.AnimateValueAsync(target, 0d, 1d, (int)Duration.TotalMilliseconds, 0,
                MotionEasing.EaseRail, progress =>
                {
                    if (outgoing is not null)
                    {
                        outgoing.Opacity = 1d - progress;
                    }

                    if (incoming is not null)
                    {
                        incoming.Opacity = progress;
                    }
                }).WaitAsync(cancellationToken);
        }
        finally
        {
            animation.Cancel(target);
            (frames as IDisposable)?.Dispose();
        }
    }
}
