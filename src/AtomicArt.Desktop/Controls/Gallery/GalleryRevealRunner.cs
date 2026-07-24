using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Controls;

using AtomicArt.Desktop.Services.Gallery;

namespace AtomicArt.Desktop.Controls.Gallery;

internal sealed class GalleryRevealRunner : GalleryOperationRunner
{
    public override Type OperationType => typeof(RevealGalleryItemOperation);
    public override bool SupportsBatching => true;

    private readonly GalleryLayoutService _galleryLayout;
    private readonly GalleryOverlayEffects _overlayEffects;
    private readonly ILogger<GalleryRevealRunner> _logger;

    public GalleryRevealRunner(
        GalleryLayoutService galleryLayout,
        GalleryOverlayEffects overlayEffects,
        ILogger<GalleryRevealRunner> logger)
    {
        _galleryLayout = galleryLayout
            ?? throw new ArgumentNullException(nameof(galleryLayout));
        _overlayEffects = overlayEffects
            ?? throw new ArgumentNullException(nameof(overlayEffects));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task RunCoreAsync(
        IReadOnlyList<GalleryOperation> operations,
        GalleryOperationCoordinator context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        GalleryOperation? operation = GalleryOperationTypeSelector.FindLast(
            operations,
            OperationType);
        int? itemIndex = operation?.ItemId is { } itemId
            ? _galleryLayout.FindItemIndex(context, itemId)
            : null;

        if ((operation?.ItemId is not { } revealedItemId)
            || (itemIndex is not { } existingItemIndex))
        {
            GalleryOperationCompletion.Complete(operations);
            return;
        }

        _galleryLayout.ScrollItemIntoView(context, existingItemIndex);
        await context.WaitForLayoutAsync();
        _galleryLayout.RefreshGalleryVirtualization(context);

        if (context.CardControls.TryGetValue(revealedItemId, out Control? control)
            && _galleryLayout.TryGetCardSurfaceRect(
                control,
                context.OverlayCanvas,
                out Rect rect))
        {
            ObserveHighlight(
                _overlayEffects.CreateRevealHighlightAsync(
                    context.OverlayCanvas,
                    rect),
                revealedItemId);
        }

        GalleryOperationCompletion.Complete(operations);
        context.NotifyStateChanged();
    }

    private void ObserveHighlight(Task highlightTask, Guid itemId)
    {
        highlightTask.ContinueWith(
            completedTask =>
            {
                _logger.LogError(
                    completedTask.Exception,
                    "Gallery reveal highlight failed for item {ItemId}.",
                    itemId);
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
