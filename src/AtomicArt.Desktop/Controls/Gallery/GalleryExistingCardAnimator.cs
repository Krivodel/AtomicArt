using Avalonia;
using Avalonia.Controls;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryExistingCardAnimator : GalleryLayoutAnimator
{
    private static readonly IReadOnlySet<Guid> EmptyIds = new HashSet<Guid>();

    public GalleryExistingCardAnimator(
        UiAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects,
        GalleryLayoutService galleryLayout)
        : base(animationScheduler, overlayEffects, galleryLayout)
    {
    }

    internal Task AnimateLayoutShiftAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        IReadOnlySet<Guid> newIds)
    {
        return AnimateAsync(
            context,
            firstSnapshot,
            newIds,
            GalleryExistingAnimationMode.Normal,
            null);
    }

    internal Task AnimateFrontMaterializationAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        IReadOnlySet<Guid> newIds,
        GalleryAnimationTracker tracker)
    {
        return AnimateAsync(
            context,
            firstSnapshot,
            newIds,
            GalleryExistingAnimationMode.ResetBeforeMeasure,
            tracker);
    }

    internal Task AnimateRemovalLayoutShiftAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        GalleryAnimationTracker tracker)
    {
        return AnimateAsync(
            context,
            firstSnapshot,
            EmptyIds,
            GalleryExistingAnimationMode.ResetBeforeMeasure,
            tracker);
    }

    internal Task AnimateResizeRetargetAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        GalleryAnimationTracker tracker)
    {
        return AnimateAsync(
            context,
            firstSnapshot,
            EmptyIds,
            GalleryExistingAnimationMode.ImmediateRetarget,
            tracker);
    }

    private static List<MovingGalleryCard> OrderMovingCards(List<MovingGalleryCard> moving)
    {
        if (moving.Count == 0)
        {
            return moving;
        }

        double minTop = moving.Min(item => item.First.Top);
        double minLeft = moving.Min(item => item.First.Left);
        double cellWidth = moving[0].First.Width + GalleryLayoutService.CardGap;
        double cellHeight = moving[0].First.Height + GalleryLayoutService.CardGap;

        return moving
            .OrderBy(item => item.First.Top)
            .ThenBy(item => item.First.Left)
            .Select(item => item with
            {
                Row = Math.Max(0, (int)Math.Round((item.First.Top - minTop) / Math.Max(1d, cellHeight))),
                Column = Math.Max(0, (int)Math.Round((item.First.Left - minLeft) / Math.Max(1d, cellWidth)))
            })
            .ToList();
    }

    private static double Distance(double x, double y)
    {
        return Math.Sqrt(x * x + y * y);
    }

    private async Task AnimateAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        IReadOnlySet<Guid> newIds,
        GalleryExistingAnimationMode mode,
        GalleryAnimationTracker? tracker)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(firstSnapshot);
        ArgumentNullException.ThrowIfNull(newIds);

        List<MovingGalleryCard> moving = CollectMovingCards(
            context,
            firstSnapshot,
            newIds,
            mode);
        if (moving.Count == 0)
        {
            return;
        }

        List<Task> animations = [];
        for (int i = 0; i < moving.Count; i++)
        {
            animations.AddRange(CreateExistingAnimations(context, moving[i], i, tracker, mode));
        }

        await Task.WhenAll(animations);
    }

    private IEnumerable<Task> CreateExistingAnimations(
        GalleryOperationCoordinator context,
        MovingGalleryCard card,
        int order,
        GalleryAnimationTracker? tracker,
        GalleryExistingAnimationMode mode)
    {
        ExistingAnimation animation = CreateExistingAnimation(context, card, order, mode);
        card.Control.ZIndex = 600 - Math.Min(order, 500);
        tracker?.Add(card.Control);

        List<Task> animations = [];
        if (tracker is null)
        {
            animations.Add(CreateWakeAnimation(context, card, order, animation));
        }

        animations.Add(CreateCardAnimation(card, animation));

        return animations;
    }

    private ExistingAnimation CreateExistingAnimation(
        GalleryOperationCoordinator context,
        MovingGalleryCard card,
        int order,
        GalleryExistingAnimationMode mode)
    {
        List<MotionFrame> frames = GalleryMotionPlanner.BuildExistingFrames(
            card.First,
            card.Last,
            card.Row,
            card.Column,
            context.OverlayCanvas.Bounds);
        int delay = CalculateExistingMoveDelay(order, card.Row, card.Column, mode);
        int duration = CalculateExistingMoveDuration(card.Dx, card.Dy, mode);
        Func<double, double> ease = mode == GalleryExistingAnimationMode.ImmediateRetarget
            ? MotionEasing.EaseOut
            : MotionEasing.EaseRail;

        return new ExistingAnimation(frames, duration, delay, ease);
    }

    private Task CreateWakeAnimation(
        GalleryOperationCoordinator context,
        MovingGalleryCard card,
        int order,
        ExistingAnimation animation)
    {
        return OverlayEffects.AnimateWakeAsync(
            context.OverlayCanvas,
            card.Last,
            order,
            animation.Frames,
            animation.Duration,
            animation.Delay);
    }

    private Task CreateCardAnimation(MovingGalleryCard card, ExistingAnimation animation)
    {
        return AnimationScheduler.AnimateAsync(
            card.Control,
            animation.Frames,
            animation.Duration,
            animation.Delay,
            animation.Ease,
            () => card.Control.ZIndex = 0);
    }

    private int CalculateExistingMoveDelay(
        int order,
        int row,
        int column,
        GalleryExistingAnimationMode mode)
    {
        if (mode == GalleryExistingAnimationMode.ImmediateRetarget)
        {
            return 0;
        }

        int baseDelay = Math.Clamp(
            GalleryMotionTimings.ExistingMoveStartDelay
            + (order * GalleryMotionTimings.ExistingMoveOrderDelay)
            + (row * GalleryMotionTimings.ExistingMoveRowDelay)
            + (column * GalleryMotionTimings.ExistingMoveColumnDelay),
            0,
            GalleryMotionTimings.ExistingMoveMaxDelay);

        return AnimationTiming.ScaleTime(baseDelay, GalleryMotionTimings.ReleaseSpeed);
    }

    private int CalculateExistingMoveDuration(double dx, double dy, GalleryExistingAnimationMode mode)
    {
        if (mode == GalleryExistingAnimationMode.ImmediateRetarget)
        {
            int resizeDuration = Math.Clamp(
                GalleryMotionTimings.ResizeMoveBaseDuration + ((int)Distance(dx, dy) / 8),
                GalleryMotionTimings.ResizeMoveMinDuration,
                GalleryMotionTimings.ResizeMoveMaxDuration);

            return AnimationTiming.ScaleTime(resizeDuration, GalleryMotionTimings.ReleaseSpeed);
        }

        int baseDuration = Math.Clamp(
            GalleryMotionTimings.ExistingMoveBaseDuration + ((int)Distance(dx, dy) / 2),
            GalleryMotionTimings.ExistingMoveMinDuration,
            GalleryMotionTimings.ExistingMoveMaxDuration);

        return AnimationTiming.ScaleTime(baseDuration, GalleryMotionTimings.ReleaseSpeed);
    }

    private List<MovingGalleryCard> CollectMovingCards(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        IReadOnlySet<Guid> newIds,
        GalleryExistingAnimationMode mode)
    {
        List<MovingGalleryCard> moving = [];
        foreach (KeyValuePair<Guid, Control> pair in context.CardControls)
        {
            if (newIds.Contains(pair.Key) || !firstSnapshot.TryGetValue(pair.Key, out Rect first))
            {
                continue;
            }

            if (mode is GalleryExistingAnimationMode.ResetBeforeMeasure or GalleryExistingAnimationMode.ImmediateRetarget)
            {
                MotionFrameApplier.Apply(pair.Value, MotionFrame.Identity);
            }

            AddMovingCard(context, pair, first, moving);
        }

        return OrderMovingCards(moving);
    }

    private void AddMovingCard(
        GalleryOperationCoordinator context,
        KeyValuePair<Guid, Control> pair,
        Rect first,
        List<MovingGalleryCard> moving)
    {
        if (!GalleryLayout.TryGetOverlayRect(pair.Value, context.OverlayCanvas, out Rect last))
        {
            return;
        }

        double dx = first.X - last.X;
        double dy = first.Y - last.Y;
        if ((Math.Abs(dx) < 0.5d) && (Math.Abs(dy) < 0.5d))
        {
            return;
        }

        moving.Add(new MovingGalleryCard(pair.Key, pair.Value, first, last, dx, dy, 0, 0));
    }
}
