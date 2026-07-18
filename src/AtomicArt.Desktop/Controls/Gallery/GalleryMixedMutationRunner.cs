using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryMixedMutationRunner : IGalleryOperationRunner
{
    public Type OperationType => typeof(MixedMutationGalleryOperation);
    public bool SupportsBatching => false;

    private readonly GalleryMotionAnimator _motionAnimator;
    private readonly GalleryLayoutService _galleryLayout;
    private readonly ILogger<GalleryMixedMutationRunner> _logger;

    public GalleryMixedMutationRunner(
        GalleryMotionAnimator motionAnimator,
        GalleryLayoutService galleryLayout,
        ILogger<GalleryMixedMutationRunner> logger)
    {
        _motionAnimator = motionAnimator ?? throw new ArgumentNullException(nameof(motionAnimator));
        _galleryLayout = galleryLayout ?? throw new ArgumentNullException(nameof(galleryLayout));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanRun(IReadOnlyList<GalleryOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return operations.Any(operation => operation.GetType() == OperationType);
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
        ct.ThrowIfCancellationRequested();

        GalleryAnimationTracker deleteOverlays = [];

        try
        {
            await ExecuteMutationAsync(context, operations, deleteOverlays);
            CompleteOperations(operations);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to apply gallery mutation.");
            FailOperations(operations, exception);
        }
        finally
        {
            RemoveOverlays(context.OverlayCanvas, deleteOverlays);
            context.NotifyStateChanged();
        }
    }

    private static void RemoveOverlays(Canvas overlayCanvas, IEnumerable<Control> overlays)
    {
        foreach (Control overlay in overlays)
        {
            overlayCanvas.Children.Remove(overlay);
        }
    }

    private static void CompleteOperations(IEnumerable<GalleryOperation> operations)
    {
        foreach (GalleryOperation operation in operations)
        {
            operation.Completion.TrySetResult();
        }
    }

    private static void FailOperations(IEnumerable<GalleryOperation> operations, Exception exception)
    {
        foreach (GalleryOperation operation in operations)
        {
            operation.Completion.TrySetException(exception);
        }
    }

    private async Task ExecuteMutationAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        GalleryAnimationTracker deleteOverlays)
    {
        _galleryLayout.SynchronizeCardControlIds(context);
        Dictionary<Guid, Rect> first = _galleryLayout.TakeSnapshot(context);
        List<(object Item, Rect Rect)> removedItems = MaterializeOperations(context, operations, first);
        await RenderMutationAsync(context);
        await Task.WhenAll(CreateAnimations(context, first, removedItems, deleteOverlays));
    }

    private async Task RenderMutationAsync(GalleryOperationCoordinator context)
    {
        _galleryLayout.RenderCards(context);
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
            _motionAnimator.AnimateLayoutShiftAsync(context, first, newIds)
        ];
        foreach ((object item, Rect rect) in removedItems)
        {
            animations.Add(_motionAnimator.AnimateRemovedItemAsync(context, item, rect, deleteOverlays));
        }

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
        for (int i = operations.Count - 1; i >= 0; i--)
        {
            if (operations[i].GetType() == OperationType)
            {
                return operations[i].Items.ToList();
            }
        }

        List<object> emptyItems = [];

        return emptyItems;
    }
}
