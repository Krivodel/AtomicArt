using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.UiAnimation;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryRemoveRunner : GalleryAnimatedOperationRunner
{
    public override Type OperationType => typeof(RemoveGalleryOperation);
    public override bool SupportsBatching => false;

    private readonly UiAnimationScheduler _animationScheduler;

    public GalleryRemoveRunner(
        UiAnimationScheduler animationScheduler,
        GalleryMotionAnimator motionAnimator,
        GalleryLayoutService galleryLayout,
        ILogger<GalleryRemoveRunner> logger)
        : base(motionAnimator, galleryLayout, logger)
    {
        _animationScheduler = animationScheduler ?? throw new ArgumentNullException(nameof(animationScheduler));
    }

    protected override async Task RunCoreAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
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

    protected override bool CanRunCore(IReadOnlyList<GalleryOperation> operations)
    {
        return GalleryOperationTypeSelector.ContainsOnly(operations, OperationType);
    }

    protected override IReadOnlyList<GalleryOperation> SelectOperationsCore(
        IReadOnlyList<GalleryOperation> operations)
    {
        return operations;
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
            MotionFrameApplier.Apply(control, MotionFrame.Identity);
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
        await RenderCardsAsync(context);
        StartRemovalAnimations(context, first, removedItems, deleteOverlays, runningMoveControls);
        ct.ThrowIfCancellationRequested();
    }

    private Dictionary<Guid, Rect> PrepareRemoval(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        out List<(object Item, Rect Rect)> removedItems)
    {
        GalleryLayout.SynchronizeCardControlIds(context);
        Dictionary<Guid, Rect> first = GalleryLayout.TakeSnapshot(context);
        List<Control> interruptedControls = context.CardControls.Values.ToList();
        _animationScheduler.Cancel(interruptedControls);
        ResetControls(interruptedControls);
        removedItems = MaterializeOperations(context, operations, first);

        return first;
    }

    private void StartRemovalAnimations(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> first,
        List<(object Item, Rect Rect)> removedItems,
        GalleryAnimationTracker deleteOverlays,
        GalleryAnimationTracker runningMoveControls)
    {
        Task moveAnimation = MotionAnimator.AnimateRemovalLayoutShiftAsync(
            context,
            first,
            runningMoveControls);
        List<Task> removeAnimations = [];
        StartRemovedItemAnimations(context, removedItems, deleteOverlays, removeAnimations.Add);
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
        Logger.LogError(exception, "Failed to remove gallery items.");
        CancelAnimations(context, runningMoveControls, deleteOverlays);
        GalleryOperationCompletion.Fail(operations, exception);
    }

    private void ObserveAnimations(IEnumerable<Task> animations)
    {
        foreach (Task animation in animations)
        {
            animation.ContinueWith(
                completedAnimation =>
                {
                    Logger.LogError(completedAnimation.Exception, "Gallery remove animation failed.");
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
