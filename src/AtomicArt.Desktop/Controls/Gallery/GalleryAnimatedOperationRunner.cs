using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;

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

    protected void StartRemovedItemAnimations(
        GalleryOperationCoordinator context,
        IEnumerable<(Control Control, Rect Rect)> removedItems,
        GalleryAnimationTracker deleteOverlays,
        Action<Task> animationStarted)
    {
        ArgumentNullException.ThrowIfNull(removedItems);
        ArgumentNullException.ThrowIfNull(animationStarted);

        foreach ((Control control, Rect rect) in removedItems)
        {
            Task animation = MotionAnimator.AnimateRemovedItemAsync(
                context,
                control,
                rect,
                deleteOverlays);
            animationStarted(animation);
        }
    }

    protected async Task RenderCardsAsync(GalleryOperationCoordinator context)
    {
        GalleryLayout.RenderCards(context);
        await context.WaitForLayoutAsync();
    }
}
