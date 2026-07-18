using Microsoft.Extensions.Logging;

using Avalonia;
using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryMixedMutationRunner : GalleryAnimatedOperationRunner
{
    public override Type OperationType => typeof(MixedMutationGalleryOperation);
    public override bool SupportsBatching => false;

    public GalleryMixedMutationRunner(
        GalleryMotionAnimator motionAnimator,
        GalleryLayoutService galleryLayout,
        ILogger<GalleryMixedMutationRunner> logger)
        : base(motionAnimator, galleryLayout, logger)
    {
    }

    protected override async Task RunCoreAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        GalleryAnimationTracker deleteOverlays = [];

        try
        {
            await ExecuteMutationAsync(context, operations, deleteOverlays);
            GalleryOperationCompletion.Complete(operations);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to apply gallery mutation.");
            GalleryOperationCompletion.Fail(operations, exception);
        }
        finally
        {
            GalleryOverlayCollection.RemoveAll(context.OverlayCanvas, deleteOverlays);
            context.NotifyStateChanged();
        }
    }

    protected override IReadOnlyList<GalleryOperation> SelectOperationsCore(
        IReadOnlyList<GalleryOperation> operations)
    {
        return operations;
    }

    private async Task ExecuteMutationAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        GalleryAnimationTracker deleteOverlays)
    {
        GalleryLayout.SynchronizeCardControlIds(context);
        Dictionary<Guid, Rect> first = GalleryLayout.TakeSnapshot(context);
        List<(object Item, Rect Rect)> removedItems = MaterializeOperations(context, operations, first);
        await RenderMutationAsync(context);
        await Task.WhenAll(CreateAnimations(context, first, removedItems, deleteOverlays));
    }

    private async Task RenderMutationAsync(GalleryOperationCoordinator context)
    {
        GalleryLayout.RenderCards(context);
        await context.WaitForLayoutAsync();
    }

    private List<Task> CreateAnimations(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> first,
        IEnumerable<(object Item, Rect Rect)> removedItems,
        GalleryAnimationTracker deleteOverlays)
    {
        HashSet<Guid> newIds = [];
        List<Task> animations =
        [
            MotionAnimator.AnimateLayoutShiftAsync(context, first, newIds)
        ];
        StartRemovedItemAnimations(context, removedItems, deleteOverlays, animations.Add);

        return animations;
    }

    private List<(object Item, Rect Rect)> MaterializeOperations(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        IReadOnlyDictionary<Guid, Rect> first)
    {
        List<object> finalItems = GetFinalItems(operations);
        HashSet<Guid> finalIds = finalItems
            .Select(context.GetItemId)
            .ToHashSet();
        List<(object Item, Rect Rect)> removedItems = [];

        foreach (object currentItem in context.Items)
        {
            Guid id = context.GetItemId(currentItem);
            if (finalIds.Contains(id))
            {
                continue;
            }

            if (first.TryGetValue(id, out Rect rect))
            {
                removedItems.Add((currentItem, rect));
            }
        }

        context.HiddenItemIds.Clear();
        context.ReplaceItems(finalItems);

        return removedItems;
    }

    private List<object> GetFinalItems(IReadOnlyList<GalleryOperation> operations)
    {
        GalleryOperation? operation = GalleryOperationTypeSelector.FindLast(
            operations,
            OperationType);

        if (operation is not null)
        {
            return operation.Items.ToList();
        }

        List<object> emptyItems = [];

        return emptyItems;
    }
}
