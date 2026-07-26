using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;
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
            MotionAnimator.ReleaseRemovedItems(context, deleteOverlays);
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
        List<(Control Control, Rect Rect)> removedItems = MaterializeOperations(
            context,
            operations,
            first,
            deleteOverlays);
        await RenderCardsAsync(context);
        await Task.WhenAll(CreateAnimations(context, first, removedItems, deleteOverlays));
    }

    private List<Task> CreateAnimations(
        GalleryOperationCoordinator context,
        Dictionary<Guid, Rect> first,
        IEnumerable<(Control Control, Rect Rect)> removedItems,
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

    private List<(Control Control, Rect Rect)> MaterializeOperations(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        IReadOnlyDictionary<Guid, Rect> first,
        GalleryAnimationTracker deleteOverlays)
    {
        List<object> finalItems = GetFinalItems(operations);
        HashSet<Guid> finalIds = finalItems
            .Select(context.GetItemId)
            .ToHashSet();
        List<(Control Control, Rect Rect)> removedItems = [];

        foreach (object currentItem in context.Items)
        {
            Guid id = context.GetItemId(currentItem);
            if (finalIds.Contains(id))
            {
                continue;
            }

            if (first.TryGetValue(id, out Rect rect))
            {
                Control? control = MotionAnimator.PrepareRemovedItem(
                    context,
                    id,
                    rect,
                    deleteOverlays);
                if (control is not null)
                {
                    removedItems.Add((control, rect));
                }
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
