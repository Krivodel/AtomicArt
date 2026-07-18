using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Controls.Gallery;

internal abstract class GalleryAnimatedOperationRunner : GalleryOperationRunner
{
    protected GalleryMotionAnimator MotionAnimator { get; }
    protected GalleryLayoutService GalleryLayout { get; }
    protected ILogger Logger { get; }

    protected GalleryAnimatedOperationRunner(
        GalleryMotionAnimator motionAnimator,
        GalleryLayoutService galleryLayout,
        ILogger logger)
    {
        MotionAnimator = motionAnimator ?? throw new ArgumentNullException(nameof(motionAnimator));
        GalleryLayout = galleryLayout ?? throw new ArgumentNullException(nameof(galleryLayout));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
