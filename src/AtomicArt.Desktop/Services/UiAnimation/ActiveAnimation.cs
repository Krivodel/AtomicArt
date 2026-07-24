using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class ActiveAnimation
{
    public Control Control { get; }
    public TimeSpan Duration { get; }
    public TimeSpan Delay { get; }
    public Func<double, double> Ease { get; }
    public Action<double> ApplyProgress { get; }
    public Action? Completed { get; }
    public TaskCompletionSource Completion { get; }
    public TimeSpan? StartTime { get; set; }

    public ActiveAnimation(
        Control control,
        TimeSpan duration,
        TimeSpan delay,
        Func<double, double> ease,
        Action<double> applyProgress,
        Action? completed,
        TaskCompletionSource completion)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        Duration = duration;
        Delay = delay;
        Ease = ease ?? throw new ArgumentNullException(nameof(ease));
        ApplyProgress = applyProgress ?? throw new ArgumentNullException(nameof(applyProgress));
        Completed = completed;
        Completion = completion ?? throw new ArgumentNullException(nameof(completion));
    }
}
