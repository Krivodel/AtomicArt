using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal abstract class GalleryOverlayAnimator : GalleryScheduledAnimator
{
    protected GalleryOverlayEffects OverlayEffects { get; }

    protected GalleryOverlayAnimator(
        GalleryAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects)
        : base(animationScheduler)
    {
        OverlayEffects = overlayEffects ?? throw new ArgumentNullException(nameof(overlayEffects));
    }
}
