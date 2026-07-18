namespace AtomicArt.Desktop.Services.GalleryAnimation;

internal interface IUiFrameScheduler
{
    void RequestAnimationFrame(Action<TimeSpan> frameAction);
}
