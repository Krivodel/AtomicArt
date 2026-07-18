using Avalonia;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryMotionAnimator
{
    private readonly GalleryAppendAnimator _appendAnimator;
    private readonly GalleryExistingCardAnimator _existingCardAnimator;
    private readonly GallerySpawnRetargetAnimator _spawnRetargetAnimator;
    private readonly GalleryRemoveAnimator _removeAnimator;

    public GalleryMotionAnimator(
        GalleryAppendAnimator appendAnimator,
        GalleryExistingCardAnimator existingCardAnimator,
        GallerySpawnRetargetAnimator spawnRetargetAnimator,
        GalleryRemoveAnimator removeAnimator)
    {
        _appendAnimator = appendAnimator ?? throw new ArgumentNullException(nameof(appendAnimator));
        _existingCardAnimator = existingCardAnimator ?? throw new ArgumentNullException(nameof(existingCardAnimator));
        _spawnRetargetAnimator = spawnRetargetAnimator ?? throw new ArgumentNullException(nameof(spawnRetargetAnimator));
        _removeAnimator = removeAnimator ?? throw new ArgumentNullException(nameof(removeAnimator));
    }

    internal Task AnimateAppendBatchAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<object> batch)
    {
        return _appendAnimator.AnimateAppendBatchAsync(context, batch);
    }

    internal Task AnimateLayoutShiftAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        IReadOnlySet<Guid> newIds)
    {
        return _existingCardAnimator.AnimateLayoutShiftAsync(
            context,
            firstSnapshot,
            newIds);
    }

    internal Task AnimateFrontMaterializationAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        IReadOnlySet<Guid> newIds,
        GalleryAnimationTracker tracker)
    {
        return _existingCardAnimator.AnimateFrontMaterializationAsync(
            context,
            firstSnapshot,
            newIds,
            tracker);
    }

    internal Task AnimateRemovalLayoutShiftAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        GalleryAnimationTracker tracker)
    {
        return _existingCardAnimator.AnimateRemovalLayoutShiftAsync(
            context,
            firstSnapshot,
            tracker);
    }

    internal Task AnimateResizeRetargetAsync(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> firstSnapshot,
        GalleryAnimationTracker tracker)
    {
        return _existingCardAnimator.AnimateResizeRetargetAsync(
            context,
            firstSnapshot,
            tracker);
    }

    internal Task AnimateSpawnRetargetAsync(
        GalleryOperationCoordinator context,
        IReadOnlyDictionary<Guid, Rect> currentSpawnRects,
        GalleryFrontGenerationRunState state)
    {
        return _spawnRetargetAnimator.AnimateSpawnRetargetAsync(
            context,
            currentSpawnRects,
            state);
    }

    internal Task AnimateRemovedItemAsync(
        GalleryOperationCoordinator context,
        object item,
        Rect rect,
        GalleryAnimationTracker deleteOverlays)
    {
        return _removeAnimator.AnimateRemovedItemAsync(
            context,
            item,
            rect,
            deleteOverlays);
    }
}
