using Avalonia;
using Avalonia.Controls;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GallerySpawnRetargetAnimator : GalleryLayoutAnimator
{
    public GallerySpawnRetargetAnimator(
        GalleryAnimationScheduler animationScheduler,
        GalleryOverlayEffects overlayEffects,
        GalleryLayoutService galleryLayout)
        : base(animationScheduler, overlayEffects, galleryLayout)
    {
    }

    internal async Task AnimateSpawnRetargetAsync(
        GalleryOperationCoordinator context,
        IReadOnlyDictionary<Guid, Rect> currentSpawnRects,
        GalleryFrontGenerationRunState state)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentSpawnRects);
        ArgumentNullException.ThrowIfNull(state);

        List<GallerySpawnTarget> targets = CollectSpawnTargets(context, state.ActiveFrontItems);
        if (targets.Count == 0)
        {
            return;
        }

        List<Task> animations = CreateSpawnRetargetAnimations(
            context,
            targets,
            currentSpawnRects,
            state);

        await Task.WhenAll(animations);
    }

    private static SpawnStartState CreateSpawnStartState(
        GallerySpawnTarget target,
        IReadOnlyDictionary<Guid, Rect> currentSpawnRects,
        Point packCenter,
        int index)
    {
        bool hasCurrentRect = currentSpawnRects.TryGetValue(target.Id, out Rect currentRect);
        Point center = hasCurrentRect ? currentRect.Center : packCenter;
        double scale = hasCurrentRect
            ? Math.Clamp(Math.Min(currentRect.Width / target.Rect.Width, currentRect.Height / target.Rect.Height), 0.30d, 1.10d)
            : 0.30d;
        int delay = hasCurrentRect
            ? 0
            : GalleryMotionTimings.SpawnDelayMilliseconds
              + AnimationTiming.ScaleTime(Math.Min(index * 32, 160), GalleryMotionTimings.SpawnSpeed);
        int duration = AnimationTiming.ScaleTime(
            hasCurrentRect ? 760 : 940,
            GalleryMotionTimings.SpawnSpeed);

        return new SpawnStartState(center, scale, hasCurrentRect ? 1d : 0d, delay, duration, hasCurrentRect);
    }

    private static void CompleteSpawnTarget(
        GalleryOperationCoordinator context,
        Guid id,
        Control clone,
        GalleryFrontGenerationRunState state)
    {
        if (context.CardControls.TryGetValue(id, out Control? control))
        {
            MotionFrameApplier.Apply(control, new MotionFrame(0d, 0d, 1d, 0d, 1d));
            control.Opacity = 1d;
        }

        context.HiddenItemIds.Remove(id);
        context.OverlayCanvas.Children.Remove(clone);
        state.OverlayControls.Remove(clone);
        state.SpawnClones.Remove(id);
    }

    private List<Task> CreateSpawnRetargetAnimations(
        GalleryOperationCoordinator context,
        IReadOnlyList<GallerySpawnTarget> targets,
        IReadOnlyDictionary<Guid, Rect> currentSpawnRects,
        GalleryFrontGenerationRunState state)
    {
        Point packCenter = GalleryMotionPlanner.GetPackCenter(targets.Select(target => target.Rect).ToList());
        List<Task> animations = CreateInitialBurst(
            context,
            targets,
            currentSpawnRects,
            state.OverlayControls,
            state.RunningControls,
            packCenter);

        for (int i = 0; i < targets.Count; i++)
        {
            animations.Add(CreateSpawnTargetAnimation(
                context,
                i,
                targets,
                currentSpawnRects,
                state,
                packCenter));
        }

        return animations;
    }

    private List<Task> CreateInitialBurst(
        GalleryOperationCoordinator context,
        IReadOnlyList<GallerySpawnTarget> targets,
        IReadOnlyDictionary<Guid, Rect> currentSpawnRects,
        GalleryAnimationTracker overlayControls,
        GalleryAnimationTracker animatedControls,
        Point packCenter)
    {
        if (targets.All(target => currentSpawnRects.ContainsKey(target.Id)))
        {
            List<Task> emptyAnimations = [];

            return emptyAnimations;
        }

        return OverlayEffects
            .CreateBurst(
                context.OverlayCanvas,
                packCenter,
                GalleryMotionTimings.SpawnSpeed,
                GalleryMotionTimings.SpawnDelayMilliseconds,
                overlayControls,
                animatedControls)
            .ToList();
    }

    private List<GallerySpawnTarget> CollectSpawnTargets(
        GalleryOperationCoordinator context,
        IReadOnlyList<object> activeFrontItems)
    {
        List<GallerySpawnTarget> targets = [];
        foreach (object item in activeFrontItems)
        {
            Guid id = context.GetItemId(item);
            if (!context.CardControls.TryGetValue(id, out Control? control))
            {
                continue;
            }

            MotionFrameApplier.Apply(control, new MotionFrame(0d, 0d, 1d, 0d, 0d));
            if (GalleryLayout.TryGetOverlayRect(control, context.OverlayCanvas, out Rect rect)
                && rect is { Width: > 0d, Height: > 0d })
            {
                targets.Add(new GallerySpawnTarget(item, id, rect));
            }
        }

        return targets;
    }

    private Task CreateSpawnTargetAnimation(
        GalleryOperationCoordinator context,
        int index,
        IReadOnlyList<GallerySpawnTarget> targets,
        IReadOnlyDictionary<Guid, Rect> currentSpawnRects,
        GalleryFrontGenerationRunState state,
        Point packCenter)
    {
        GallerySpawnTarget target = targets[index];
        SpawnStartState startState = CreateSpawnStartState(target, currentSpawnRects, packCenter, index);
        List<Task> animations = CreateTargetFlash(
            context,
            index,
            target,
            startState.HasCurrentRect,
            state.OverlayControls,
            state.RunningControls);
        Control clone = CreateSpawnClone(context, target, state);
        animations.Add(CreateFlightAnimation(
            context,
            index,
            targets.Count,
            target,
            startState,
            packCenter,
            clone,
            state));

        return Task.WhenAll(animations);
    }

    private Task CreateFlightAnimation(
        GalleryOperationCoordinator context,
        int index,
        int targetCount,
        GallerySpawnTarget target,
        SpawnStartState startState,
        Point packCenter,
        Control clone,
        GalleryFrontGenerationRunState state)
    {
        List<MotionFrame> frames = GalleryMotionPlanner.BuildFlightFramesFromCurrent(
            index,
            targetCount,
            target.Rect,
            startState.Center,
            startState.Scale,
            startState.Opacity,
            packCenter);

        return AnimationScheduler.AnimateAsync(
            clone,
            frames,
            startState.Duration,
            startState.Delay,
            MotionEasing.EaseSpawn,
            () => CompleteSpawnTarget(context, target.Id, clone, state));
    }

    private List<Task> CreateTargetFlash(
        GalleryOperationCoordinator context,
        int index,
        GallerySpawnTarget target,
        bool hasCurrentRect,
        GalleryAnimationTracker overlayControls,
        GalleryAnimationTracker animatedControls)
    {
        if (hasCurrentRect)
        {
            List<Task> emptyAnimations = [];

            return emptyAnimations;
        }

        List<Task> animations =
        [
            OverlayEffects.CreateTargetFlash(
                context.OverlayCanvas,
                target.Rect,
                index,
                GalleryMotionTimings.SpawnSpeed,
                GalleryMotionTimings.SpawnDelayMilliseconds,
                overlayControls,
                animatedControls)
        ];

        return animations;
    }

    private Control CreateSpawnClone(
        GalleryOperationCoordinator context,
        GallerySpawnTarget target,
        GalleryFrontGenerationRunState state)
    {
        Control clone = OverlayEffects.CreateOverlayCard(context, target.Item, target.Rect);
        state.SpawnClones[target.Id] = clone;
        state.OverlayControls.Add(clone);
        state.RunningControls.Add(clone);

        return clone;
    }

    private sealed record SpawnStartState(
        Point Center,
        double Scale,
        double Opacity,
        int Delay,
        int Duration,
        bool HasCurrentRect);
}
