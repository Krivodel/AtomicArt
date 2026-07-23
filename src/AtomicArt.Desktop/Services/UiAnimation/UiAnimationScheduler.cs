using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class UiAnimationScheduler
{
    public bool HasActiveAnimations
    {
        get
        {
            return _animations.Count > 0;
        }
    }

    private readonly IUiFrameScheduler _frameScheduler;
    private readonly Action<Control, MotionFrame> _applyFrame;
    private readonly List<ActiveAnimation> _animations = [];
    private bool _isRunning;

    public UiAnimationScheduler(IUiFrameScheduler frameScheduler)
        : this(frameScheduler, MotionFrameApplier.Apply)
    {
    }

    internal UiAnimationScheduler(
        IUiFrameScheduler frameScheduler,
        Action<Control, MotionFrame> applyFrame)
    {
        _frameScheduler = frameScheduler ?? throw new ArgumentNullException(nameof(frameScheduler));
        _applyFrame = applyFrame ?? throw new ArgumentNullException(nameof(applyFrame));
    }

    public void Cancel(IEnumerable<Control> controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        HashSet<Control> controlSet = controls.ToHashSet();
        if (controlSet.Count == 0)
        {
            return;
        }

        for (int i = _animations.Count - 1; i >= 0; i--)
        {
            ActiveAnimation animation = _animations[i];
            if (!controlSet.Contains(animation.Control))
            {
                continue;
            }

            _animations.RemoveAt(i);
            animation.Completion.TrySetResult();
        }

        if (_animations.Count == 0)
        {
            _isRunning = false;
        }
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        _frameScheduler.RequestAnimationFrame(frameAction);
    }

    public Task AnimateAsync(
        Control control,
        IReadOnlyList<MotionFrame> frames,
        int durationMilliseconds,
        int delayMilliseconds,
        Func<double, double> ease,
        Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(ease);

        if (frames.Count == 0)
        {
            completed?.Invoke();
            return Task.CompletedTask;
        }

        if (durationMilliseconds <= 0)
        {
            _applyFrame(control, frames[^1]);
            completed?.Invoke();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ActiveAnimation animation = new(
            control,
            frames.ToArray(),
            TimeSpan.FromMilliseconds(durationMilliseconds),
            TimeSpan.FromMilliseconds(Math.Max(0, delayMilliseconds)),
            ease,
            completed,
            completion);

        _applyFrame(control, animation.Frames[0]);
        _animations.Add(animation);
        StartIfNeeded();

        return completion.Task;
    }

    private static MotionFrame Interpolate(IReadOnlyList<MotionFrame> frames, double progress)
    {
        if (frames.Count == 1)
        {
            return frames[0];
        }

        double scaled = progress * (frames.Count - 1);
        int index = Math.Min((int)Math.Floor(scaled), frames.Count - 2);
        double local = scaled - index;
        MotionFrame from = frames[index];
        MotionFrame to = frames[index + 1];

        return new MotionFrame(
            Lerp(from.X, to.X, local),
            Lerp(from.Y, to.Y, local),
            Lerp(from.Scale, to.Scale, local),
            Lerp(from.Rotate, to.Rotate, local),
            Lerp(from.Opacity, to.Opacity, local));
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + ((to - from) * amount);
    }

    private void StartIfNeeded()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _frameScheduler.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan now)
    {
        for (int i = _animations.Count - 1; i >= 0; i--)
        {
            ActiveAnimation animation = _animations[i];
            animation.StartTime ??= now;

            TimeSpan elapsed = now - animation.StartTime.Value;
            if (elapsed < animation.Delay)
            {
                continue;
            }

            double rawProgress = Math.Clamp(
                (elapsed - animation.Delay).TotalMilliseconds / animation.Duration.TotalMilliseconds,
                0d,
                1d);
            MotionFrame frame = Interpolate(animation.Frames, animation.Ease(rawProgress));
            _applyFrame(animation.Control, frame);

            if (rawProgress >= 1d)
            {
                _animations.RemoveAt(i);
                animation.Completed?.Invoke();
                animation.Completion.TrySetResult();
            }
        }

        if (_animations.Count == 0)
        {
            _isRunning = false;
            return;
        }

        _frameScheduler.RequestAnimationFrame(OnFrame);
    }
}
