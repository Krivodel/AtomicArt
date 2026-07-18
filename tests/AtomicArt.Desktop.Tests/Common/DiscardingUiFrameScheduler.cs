using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests;

internal sealed class DiscardingUiFrameScheduler : IUiFrameScheduler
{
    public void RequestAnimationFrame(Action<TimeSpan> frameAction)
    {
        ArgumentNullException.ThrowIfNull(frameAction);
    }
}
