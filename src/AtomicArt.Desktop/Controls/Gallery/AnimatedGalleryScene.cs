using Microsoft.Extensions.Logging;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class AnimatedGalleryScene : IDisposable
{
    internal GalleryLayoutService GalleryLayout { get; }
    internal GalleryAnimationScheduler AnimationScheduler { get; }
    internal GalleryMotionAnimator MotionAnimator { get; }
    internal GalleryOperationCoordinator OperationCoordinator { get; }
    internal IGalleryCardControlFactory CardControlFactory { get; }
    internal ILogger<AnimatedGalleryResizeController> ResizeLogger { get; }

    private IDisposable? _lifetime;

    public AnimatedGalleryScene(
        GalleryLayoutService galleryLayout,
        GalleryAnimationScheduler animationScheduler,
        GalleryMotionAnimator motionAnimator,
        GalleryOperationCoordinator operationCoordinator,
        IGalleryCardControlFactory cardControlFactory,
        ILogger<AnimatedGalleryResizeController> resizeLogger)
    {
        GalleryLayout = galleryLayout ?? throw new ArgumentNullException(nameof(galleryLayout));
        AnimationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
        MotionAnimator = motionAnimator ?? throw new ArgumentNullException(nameof(motionAnimator));
        OperationCoordinator = operationCoordinator ?? throw new ArgumentNullException(nameof(operationCoordinator));
        CardControlFactory = cardControlFactory ?? throw new ArgumentNullException(nameof(cardControlFactory));
        ResizeLogger = resizeLogger ?? throw new ArgumentNullException(nameof(resizeLogger));
    }

    internal void AttachLifetime(IDisposable lifetime)
    {
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    public void Dispose()
    {
        IDisposable? lifetime = _lifetime;
        _lifetime = null;
        lifetime?.Dispose();
    }
}
