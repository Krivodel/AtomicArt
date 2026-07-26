using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryRemoveAnimator : GalleryOverlayAnimator
{
    private const int RemoveDurationMilliseconds = 520;

    public GalleryRemoveAnimator(
        UiAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects)
        : base(animationScheduler, overlayEffects)
    {
    }

    internal Control? PrepareRemovedItem(
        GalleryOperationCoordinator context,
        Guid itemId,
        Rect rect,
        GalleryAnimationTracker deleteOverlays)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(deleteOverlays);

        if (!context.CardControls.TryGetValue(itemId, out Control? control))
        {
            return null;
        }

        PrepareForRemovalTransfer(control);
        OverlayEffects.MoveCardToOverlay(context, control, rect);
        context.CardControls.Remove(itemId);
        deleteOverlays.Add(control);

        return control;
    }

    internal Task AnimateRemovedItemAsync(
        GalleryOperationCoordinator context,
        Control control,
        Rect rect,
        GalleryAnimationTracker deleteOverlays)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(deleteOverlays);

        BeginRemovalAnimation(control);
        double sign = rect.Center.X > (context.OverlayCanvas.Bounds.Width / 2d) ? 1d : -1d;

        return AnimationScheduler.AnimateAsync(
            control,
            CreateRemoveFrames(sign),
            RemoveDurationMilliseconds,
            0,
            MotionEasing.EaseMaterial,
            () => ReleaseRemovedItem(context, deleteOverlays, control));
    }

    internal void ReleaseRemovedItems(
        GalleryOperationCoordinator context,
        GalleryAnimationTracker deleteOverlays)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(deleteOverlays);

        List<Control> controls = deleteOverlays.ToList();
        AnimationScheduler.Cancel(controls);

        foreach (Control control in controls)
        {
            ReleaseRemovedItem(context, deleteOverlays, control);
        }
    }

    private static void PrepareForRemovalTransfer(Control control)
    {
        if (control is IGalleryRemovalAnimationParticipant participant)
        {
            participant.PrepareForRemovalTransfer();
        }
    }

    private static void BeginRemovalAnimation(Control control)
    {
        if (control is IGalleryRemovalAnimationParticipant participant)
        {
            participant.BeginRemovalAnimation(RemoveDurationMilliseconds);
        }
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

    private static void ReleaseRemovedItem(
        GalleryOperationCoordinator context,
        GalleryAnimationTracker deleteOverlays,
        Control control)
    {
        if (!deleteOverlays.Contains(control))
        {
            return;
        }

        deleteOverlays.Remove(control);
        context.OverlayCanvas.Children.Remove(control);
        context.RecycleControl(control);
    }
}
