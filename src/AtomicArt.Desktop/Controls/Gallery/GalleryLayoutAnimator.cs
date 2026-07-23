using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal abstract class GalleryLayoutAnimator : GalleryOverlayAnimator
{
    protected GalleryLayoutService GalleryLayout { get; }

    protected GalleryLayoutAnimator(
        UiAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects,
        GalleryLayoutService galleryLayout)
        : base(animationScheduler, overlayEffects)
    {
        GalleryLayout = galleryLayout ?? throw new ArgumentNullException(nameof(galleryLayout));
    }
}
