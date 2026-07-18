using Microsoft.Extensions.Logging;

using Avalonia;

using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryAppendRunner : GalleryOperationRunner
{
    public override Type OperationType => typeof(AppendBatchGalleryOperation);
    public override bool SupportsBatching => true;

    private readonly GalleryMotionAnimator _motionAnimator;
    private readonly GalleryLayoutService _galleryLayout;
    private readonly ILogger<GalleryAppendRunner> _logger;

    public GalleryAppendRunner(
        GalleryMotionAnimator motionAnimator,
        GalleryLayoutService galleryLayout,
        ILogger<GalleryAppendRunner> logger)
    {
        _motionAnimator = motionAnimator ?? throw new ArgumentNullException(nameof(motionAnimator));
        _galleryLayout = galleryLayout ?? throw new ArgumentNullException(nameof(galleryLayout));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task RunAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        _galleryLayout.SynchronizeCardControlIds(context);
        Dictionary<Guid, Rect> first = _galleryLayout.TakeSnapshot(context);
        AppendMaterialization materialization = MaterializeOperations(context, operations);

        if (materialization.NewIds.Count == 0)
        {
            GalleryOperationCompletion.Complete(operations);
            return;
        }

        _galleryLayout.RenderCards(context);
        await context.WaitForLayoutAsync();
        await AnimateAsync(context, operations, first, materialization.Batches, materialization.NewIds);
    }

    private static AppendMaterialization MaterializeOperations(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations)
    {
        List<List<object>> batches = [];
        HashSet<Guid> newIds = [];

        foreach (GalleryOperation operation in operations)
        {
            AddBatch(context, operation.Items.ToList(), batches, newIds);
        }

        return new AppendMaterialization(batches, newIds);
    }

    private static void AddBatch(
        GalleryOperationCoordinator context,
        List<object> batch,
        List<List<object>> batches,
        HashSet<Guid> newIds)
    {
        if (batch.Count == 0)
        {
            return;
        }

        foreach (object item in batch)
        {
            newIds.Add(context.GetItemId(item));
        }

        batches.Add(batch);
        context.AddItemsToEnd(batch);
    }

    private async Task AnimateAsync(
        GalleryOperationCoordinator context,
        IReadOnlyList<GalleryOperation> operations,
        Dictionary<Guid, Rect> first,
        List<List<object>> appendBatches,
        HashSet<Guid> allNewIds)
    {
        List<Task> animations =
        [
            _motionAnimator.AnimateLayoutShiftAsync(context, first, allNewIds)
        ];
        foreach (List<object> batch in appendBatches)
        {
            animations.Add(_motionAnimator.AnimateAppendBatchAsync(context, batch));
        }

        try
        {
            await Task.WhenAll(animations);
            GalleryOperationCompletion.Complete(operations);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to append gallery items.");
            GalleryOperationCompletion.Fail(operations, exception);
        }
    }

    private sealed record AppendMaterialization(
        List<List<object>> Batches,
        HashSet<Guid> NewIds);
}
