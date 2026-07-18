using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal abstract class GalleryScheduledAnimator
{
    protected GalleryAnimationScheduler AnimationScheduler { get; }

    protected GalleryScheduledAnimator(GalleryAnimationScheduler animationScheduler)
    {
        AnimationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
    }
}
