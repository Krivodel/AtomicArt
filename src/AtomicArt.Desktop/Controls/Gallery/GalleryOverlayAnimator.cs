using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal abstract class GalleryOverlayAnimator : GalleryScheduledAnimator
{
    protected GalleryOverlayEffects OverlayEffects { get; }

    protected GalleryOverlayAnimator(
        UiAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects)
        : base(animationScheduler)
    {
        OverlayEffects = overlayEffects ?? throw new ArgumentNullException(nameof(overlayEffects));
    }
}
