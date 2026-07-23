using Avalonia.Controls;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class AvaloniaUiFrameScheduler : IUiFrameScheduler
{
    private readonly TopLevel _topLevel;

    public AvaloniaUiFrameScheduler(TopLevel topLevel)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        _topLevel.RequestAnimationFrame(frameAction);
    }
}
