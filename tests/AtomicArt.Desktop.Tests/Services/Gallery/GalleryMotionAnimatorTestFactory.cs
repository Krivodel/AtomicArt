using AtomicArt.Desktop.Controls.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

internal static class GalleryMotionAnimatorTestFactory
{
    public static GalleryMotionAnimator Create(
        GalleryAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects,
        GalleryLayoutService galleryLayout)
    {
        return new GalleryMotionAnimator(
            new GalleryAppendAnimator(animationScheduler),
            new GalleryExistingCardAnimator(animationScheduler, overlayEffects, galleryLayout),
            new GallerySpawnRetargetAnimator(animationScheduler, overlayEffects, galleryLayout),
            new GalleryRemoveAnimator(animationScheduler, overlayEffects));
    }
}
