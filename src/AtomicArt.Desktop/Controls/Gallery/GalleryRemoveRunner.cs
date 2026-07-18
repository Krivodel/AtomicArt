using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.GalleryAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryRemoveRunner : IGalleryOperationRunner
{
    public Type OperationType => typeof(RemoveGalleryOperation);
    public bool SupportsBatching => false;

    private readonly GalleryAnimationScheduler _animationScheduler;
    private readonly GalleryMotionAnimator _motionAnimator;
    private readonly GalleryLayoutService _galleryLayout;
    private readonly ILogger<GalleryRemoveRunner> _logger;

    public GalleryRemoveRunner(
        GalleryAnimationScheduler animationScheduler,
        GalleryMotionAnimator motionAnimator,
        GalleryLayoutService galleryLayout,
        ILogger<GalleryRemoveRunner> logger)
    {
        _animationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
        _motionAnimator = motionAnimator ?? throw new ArgumentNullException(nameof(motionAnimator));
        _galleryLayout = galleryLayout ?? throw new ArgumentNullException(nameof(galleryLayout));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanRun(IReadOnlyList<GalleryOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return GalleryOperationTypeSelector.ContainsOnly(operations, OperationType);
    }

    public IReadOnlyList<GalleryOperation> SelectOperations(IReadOnlyList<GalleryOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return operations;
    }

    public async Task RunAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(context);

        GalleryAnimationTracker deleteOverlays = [];
        GalleryAnimationTracker runningMoveControls = [];

        try
        {
            await ExecuteRemovalAsync(context, operations, deleteOverlays, runningMoveControls, ct);
            GalleryOperationCompletion.Complete(operations);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            HandleCancellation(context, operations, runningMoveControls, deleteOverlays, ct);
        }
        catch (Exception exception)
        {
            HandleFailure(context, operations, runningMoveControls, deleteOverlays, exception);
        }
        finally
        {
            context.NotifyStateChanged();
        }
    }

    private static List<(object Item, Rect Rect)> MaterializeOperations(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        IReadOnlyDictionary<Guid, Rect> first)
    {
        List<(object Item, Rect Rect)> removedItems = [];
        HashSet<Guid> removedIds = [];

        foreach (GalleryOperation operation in operations)
        {
            if (operation.ItemId is not { } itemId)
            {
                continue;
            }

            object? item = context.RemoveItem(itemId);
            if (item is null)
            {
                continue;
            }

            if (removedIds.Add(itemId) && first.TryGetValue(itemId, out Rect rect))
            {
                removedItems.Add((item, rect));
            }
        }

        return removedItems;
    }

    private static void ResetControls(IEnumerable<Control> controls)
    {
        foreach (Control control in controls)
        {
            MotionFrameApplier.Apply(control, new MotionFrame(0d, 0d, 1d, 0d, 1d));
            control.ZIndex = 0;
        }
    }

    private async Task ExecuteRemovalAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        GalleryAnimationTracker deleteOverlays,
        GalleryAnimationTracker runningMoveControls,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Dictionary<Guid, Rect> first = PrepareRemoval(context, operations, out List<(object Item, Rect Rect)> removedItems);
        await RenderAfterRemovalAsync(context);
        StartRemovalAnimations(context, first, removedItems, deleteOverlays, runningMoveControls);
        ct.ThrowIfCancellationRequested();
    }

    private Dictionary<Guid, Rect> PrepareRemoval(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        out List<(object Item, Rect Rect)> removedItems)
    {
        _galleryLayout.SynchronizeCardControlIds(context);
        Dictionary<Guid, Rect> first = _galleryLayout.TakeSnapshot(context);
        List<Control> interruptedControls = context.CardControls.Values.ToList();
        _animationScheduler.Cancel(interruptedControls);
        ResetControls(interruptedControls);
        removedItems = MaterializeOperations(context, operations, first);

        return first;
    }

    private async Task RenderAfterRemovalAsync(GalleryOperationCoordinator context)
    {
        _galleryLayout.RenderCards(context);
        await context.WaitForLayoutAsync();
    }

    private void StartRemovalAnimations(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> first,
        List<(object Item, Rect Rect)> removedItems,
        GalleryAnimationTracker deleteOverlays,
        GalleryAnimationTracker runningMoveControls)
    {
        Task moveAnimation = _motionAnimator.AnimateRemovalLayoutShiftAsync(
            context,
            first,
            runningMoveControls);
        List<Task> removeAnimations = StartRemoveAnimations(context, removedItems, deleteOverlays);
        ObserveAnimations(removeAnimations.Prepend(moveAnimation));
    }

    private void CancelAnimations(
        GalleryOperationCoordinator context,
        IEnumerable<Control> runningMoveControls,
        IEnumerable<Control> deleteOverlays)
    {
        _animationScheduler.Cancel(runningMoveControls.Concat(deleteOverlays));
        GalleryOverlayCollection.RemoveAll(context.OverlayCanvas, deleteOverlays);
    }

    private void HandleCancellation(
        GalleryOperationCoordinator context,
        IEnumerable<GalleryOperation> operations,
        IEnumerable<Control> runningMoveControls,
        IEnumerable<Control> deleteOverlays,
        CancellationToken ct)
    {
        CancelAnimations(context, runningMoveControls, deleteOverlays);
        GalleryOperationCompletion.Cancel(operations, ct);
    }

    private void HandleFailure(
        GalleryOperationCoordinator context,
        IEnumerable<GalleryOperation> operations,
        IEnumerable<Control> runningMoveControls,
        IEnumerable<Control> deleteOverlays,
        Exception exception)
    {
        _logger.LogError(exception, "Failed to remove gallery items.");
        CancelAnimations(context, runningMoveControls, deleteOverlays);
        GalleryOperationCompletion.Fail(operations, exception);
    }

    private List<Task> StartRemoveAnimations(
        GalleryOperationCoordinator context,
        List<(object Item, Rect Rect)> removedItems,
        GalleryAnimationTracker deleteOverlays)
    {
        List<Task> animations = [];
        foreach ((object item, Rect rect) in removedItems)
        {
            animations.Add(_motionAnimator.AnimateRemovedItemAsync(context, item, rect, deleteOverlays));
        }

        return animations;
    }

    private void ObserveAnimations(IEnumerable<Task> animations)
    {
        foreach (Task animation in animations)
        {
            animation.ContinueWith(
                completedAnimation =>
                {
                    _logger.LogError(completedAnimation.Exception, "Gallery remove animation failed.");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
