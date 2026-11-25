using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class UiAnimationScheduler
{
    public bool HasActiveAnimations => _animations.Any(animation => !animation.Completion.Task.IsCompleted);

    private readonly IUiFrameScheduler _frameScheduler;
    private readonly Action<Control, MotionFrame> _applyFrame;
    private readonly List<ActiveAnimation> _animations = [];
    private bool _isRunning;
    private bool _isProcessingFrame;

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

        CancelAnimations(controlSet.Contains);
    }

    public void Cancel(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        CancelAnimations(candidate => ReferenceEquals(candidate, control));
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

        MotionFrame[] animationFrames = frames.ToArray();

        return AnimateProgressAsync(
            control,
            durationMilliseconds,
            delayMilliseconds,
            ease,
            progress => _applyFrame(
                control,
                Interpolate(animationFrames, progress)),
            completed);
    }

    public Task AnimateValueAsync(
        Control control,
        double fromValue,
        double toValue,
        int durationMilliseconds,
        int delayMilliseconds,
        Func<double, double> ease,
        Action<double> applyValue,
        Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(ease);
        ArgumentNullException.ThrowIfNull(applyValue);

        return AnimateProgressAsync(
            control,
            durationMilliseconds,
            delayMilliseconds,
            ease,
            progress => applyValue(Lerp(fromValue, toValue, progress)),
            completed);
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

    private void CancelAnimations(Predicate<Control> matches)
    {
        for (int i = _animations.Count - 1; i >= 0; i--)
        {
            ActiveAnimation animation = _animations[i];

            if (!matches(animation.Control))
            {
                continue;
            }

            if (!_isProcessingFrame)
            {
                _animations.RemoveAt(i);
            }

            animation.Completion.TrySetResult();
        }
    }

    private Task AnimateProgressAsync(
        Control control,
        int durationMilliseconds,
        int delayMilliseconds,
        Func<double, double> ease,
        Action<double> applyProgress,
        Action? completed)
    {
        if (durationMilliseconds <= 0)
        {
            applyProgress(1d);
            completed?.Invoke();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ActiveAnimation animation = new(
            control,
            TimeSpan.FromMilliseconds(durationMilliseconds),
            TimeSpan.FromMilliseconds(Math.Max(0, delayMilliseconds)),
            ease,
            applyProgress,
            completed,
            completion);

        animation.ApplyProgress(0d);
        _animations.Add(animation);
        StartIfNeeded();

        return completion.Task;
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
        _isProcessingFrame = true;

        try
        {
            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                ActiveAnimation animation = _animations[i];

                if (animation.Completion.Task.IsCompleted)
                {
                    continue;
                }

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
                double easedProgress = animation.Ease(rawProgress);

                if (animation.Completion.Task.IsCompleted)
                {
                    continue;
                }

                animation.ApplyProgress(easedProgress);

                if (rawProgress >= 1d && !animation.Completion.Task.IsCompleted)
                {
                    _animations.RemoveAt(i);
                    animation.Completed?.Invoke();
                    animation.Completion.TrySetResult();
                }
            }
        }
        finally
        {
            _animations.RemoveAll(animation => animation.Completion.Task.IsCompleted);
            _isProcessingFrame = false;
        }

        if (_animations.Count == 0)
        {
            _isRunning = false;
            return;
        }

        _frameScheduler.RequestAnimationFrame(OnFrame);
    }
}
