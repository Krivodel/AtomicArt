using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class ActiveAnimation
{
    public Control Control { get; }
    public MotionFrame[] Frames { get; }
    public TimeSpan Duration { get; }
    public TimeSpan Delay { get; }
    public Func<double, double> Ease { get; }
    public Action? Completed { get; }
    public TaskCompletionSource Completion { get; }
    public TimeSpan? StartTime { get; set; }

    public ActiveAnimation(
        Control control,
        MotionFrame[] frames,
        TimeSpan duration,
        TimeSpan delay,
        Func<double, double> ease,
        Action? completed,
        TaskCompletionSource completion)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        Frames = frames ?? throw new ArgumentNullException(nameof(frames));
        Duration = duration;
        Delay = delay;
        Ease = ease ?? throw new ArgumentNullException(nameof(ease));
        Completed = completed;
        Completion = completion ?? throw new ArgumentNullException(nameof(completion));
    }
}
