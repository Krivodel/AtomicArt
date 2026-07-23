using Avalonia;
using Avalonia.Controls;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryRemoveAnimator : GalleryOverlayAnimator
{
    public GalleryRemoveAnimator(
        UiAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects)
        : base(animationScheduler, overlayEffects)
    {
    }

    internal Task AnimateRemovedItemAsync(
        GalleryOperationCoordinator context,
        object item,
        Rect rect,
        GalleryAnimationTracker deleteOverlays)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(deleteOverlays);

        Control ghost = OverlayEffects.CreateOverlayCard(context, item, rect);
        deleteOverlays.Add(ghost);
        double sign = rect.Center.X > (context.OverlayCanvas.Bounds.Width / 2d) ? 1d : -1d;

        return AnimationScheduler.AnimateAsync(
            ghost,
            CreateRemoveFrames(sign),
            520,
            0,
            MotionEasing.EaseMaterial,
            () => CompleteRemoveAnimation(context, deleteOverlays, ghost));
    }

    private static List<MotionFrame> CreateRemoveFrames(double sign)
    {
        List<MotionFrame> frames =
        [
            MotionFrame.Identity,
            new(sign * 10d, -8d, 1.02d, sign * 2.5d, 1d),
            new(sign * 24d, -18d, 0.95d, sign * 5.5d, 0.92d),
            new(sign * 38d, -30d, 0.72d, sign * 8.5d, 0d)
        ];

        return frames;
    }

    private static void CompleteRemoveAnimation(
        GalleryOperationCoordinator context,
        GalleryAnimationTracker deleteOverlays,
        Control ghost)
    {
        context.OverlayCanvas.Children.Remove(ghost);
        deleteOverlays.Remove(ghost);
    }
}
