using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal abstract class GalleryScheduledAnimator
{
    protected UiAnimationScheduler AnimationScheduler { get; }

    protected GalleryScheduledAnimator(UiAnimationScheduler animationScheduler)
    {
        AnimationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
    }
}
