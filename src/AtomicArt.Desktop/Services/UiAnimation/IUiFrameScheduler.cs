namespace AtomicArt.Desktop.Services.UiAnimation;

internal interface IUiFrameScheduler
{
    void RequestAnimationFrame(Action<TimeSpan> frameAction);
}
