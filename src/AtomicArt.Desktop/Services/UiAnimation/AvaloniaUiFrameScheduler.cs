using Avalonia.Controls;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.UiAnimation;

internal sealed class AvaloniaUiFrameScheduler : IUiFrameScheduler
{
    private readonly TopLevel _topLevel;
    private readonly TopLevelPresentationObserver _presentationObserver;
    private readonly ViewerAnimationFrameScheduler _frameScheduler;

    public AvaloniaUiFrameScheduler(TopLevel topLevel)
        : this(
            topLevel,
            frameAction => topLevel.RequestAnimationFrame(frameAction))
    {
    }

    internal AvaloniaUiFrameScheduler(
        TopLevel topLevel,
        Action<Action<TimeSpan>> requestAnimationFrame)
    {
        _topLevel = topLevel ?? throw new ArgumentNullException(nameof(topLevel));
        _presentationObserver = new TopLevelPresentationObserver(
            OnPresentationChanged);
        _frameScheduler = new ViewerAnimationFrameScheduler(
            requestAnimationFrame);
    }

    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);

        EnsurePresentationObserverAttached();
        _frameScheduler.SetPresentation(_presentationObserver.IsPresented);
        _frameScheduler.RequestAnimationFrame(
            frameTime => Complete(frameAction, frameTime));
    }

    private void EnsurePresentationObserverAttached()
    {
        if (!_presentationObserver.IsAttached)
        {
            _presentationObserver.Attach(_topLevel);
        }
    }

    private void OnPresentationChanged(bool isPresented)
    {
        _frameScheduler.SetPresentation(isPresented);
    }

    private void Complete(
        Action<TimeSpan> frameAction,
        TimeSpan frameTime)
    {
        if (!_frameScheduler.HasPendingFrames)
        {
            _presentationObserver.Detach();
        }

        frameAction(frameTime);
    }
}
