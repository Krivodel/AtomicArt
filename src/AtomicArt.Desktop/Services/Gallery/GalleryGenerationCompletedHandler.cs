using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Services.Gallery;

public sealed class GalleryGenerationCompletedHandler : IGalleryLifecycleEventHandler
{
    public GenerationLifecycleStatus Status => GenerationLifecycleStatus.Completed;

    private readonly ITrustedImageFileService _trustedImageFileService;
    private readonly IGenerationResultStorage _generationResultStorage;
    private readonly IGalleryThumbnailStorage _galleryThumbnailStorage;
    private readonly IGenerationImageContentValidator _generationImageContentValidator;
    private readonly IGenerationItemStatusDescriptorRegistry _statusDescriptorRegistry;
    private readonly IGalleryLifecycleViewState _viewState;
    private readonly IGalleryStateService _galleryStateService;
    private readonly ILogger<GalleryGenerationCompletedHandler> _logger;

    public GalleryGenerationCompletedHandler(
        ITrustedImageFileService trustedImageFileService,
        IGenerationResultStorage generationResultStorage,
        IGalleryThumbnailStorage galleryThumbnailStorage,
        IGenerationImageContentValidator generationImageContentValidator,
        IGenerationItemStatusDescriptorRegistry statusDescriptorRegistry,
        IGalleryLifecycleViewState viewState,
        IGalleryStateService galleryStateService,
        ILogger<GalleryGenerationCompletedHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(trustedImageFileService);
        ArgumentNullException.ThrowIfNull(generationResultStorage);
        ArgumentNullException.ThrowIfNull(galleryThumbnailStorage);
        ArgumentNullException.ThrowIfNull(generationImageContentValidator);
        ArgumentNullException.ThrowIfNull(statusDescriptorRegistry);
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(galleryStateService);
        ArgumentNullException.ThrowIfNull(logger);

        _trustedImageFileService = trustedImageFileService;
        _generationResultStorage = generationResultStorage;
        _galleryThumbnailStorage = galleryThumbnailStorage;
        _generationImageContentValidator = generationImageContentValidator;
        _statusDescriptorRegistry = statusDescriptorRegistry;
        _viewState = viewState;
        _galleryStateService = galleryStateService;
        _logger = logger;
    }

    private bool IsGenerated(GenerationItemDto item)
    {
        return _statusDescriptorRegistry.Get(item.Status).ResultContentPolicy
            == GenerationResultContentPolicy.SaveValidatedContent;
    }

    public async Task HandleAsync(GenerationLifecycleEvent lifecycleEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        if (lifecycleEvent.Batch is null)
        {
            _logger.LogWarning(
                "Gallery completion for correlation {CorrelationId} did not include a generation batch",
                lifecycleEvent.CorrelationId);

            return;
        }

        _logger.LogInformation(
            "Gallery is applying completed batch {BatchId} with {ItemCount} items for correlation {CorrelationId}",
            lifecycleEvent.Batch.BatchId,
            lifecycleEvent.Batch.Items.Count,
            lifecycleEvent.CorrelationId);
        List<GalleryCompletedItemUpdate> itemUpdates = [];

        foreach (GenerationItemDto item in lifecycleEvent.Batch.Items)
        {
            GalleryCompletedItemUpdate itemUpdate = await CreateItemUpdateAsync(
                    lifecycleEvent.Batch.BatchId,
                    item,
                    ct)
                .ConfigureAwait(false);
            itemUpdates.Add(itemUpdate);
        }

        await _viewState
            .ApplyCompletedAsync(lifecycleEvent.CorrelationId, itemUpdates, ct)
            .ConfigureAwait(false);
        IReadOnlyList<GalleryItemState> snapshot = await _viewState
            .CreateStateSnapshotAsync(ct)
            .ConfigureAwait(false);
        await _galleryStateService.SaveAsync(snapshot, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Gallery applied completed batch {BatchId}; snapshot contains {ItemCount} items",
            lifecycleEvent.Batch.BatchId,
            snapshot.Count);
    }

    private async Task<GalleryCompletedItemUpdate> CreateItemUpdateAsync(
        Guid batchId,
        GenerationItemDto item,
        CancellationToken ct)
    {
        if (IsGenerated(item) && item.ImageContent is not null)
        {
            return await CreateGeneratedContentItemUpdateAsync(batchId, item, item.ImageContent, ct)
                .ConfigureAwait(false);
        }

        string? trustedImagePath = GetLegacyTrustedImagePathOrDefault(item);

        return new GalleryCompletedItemUpdate(item, trustedImagePath, null);
    }

    private async Task<GalleryCompletedItemUpdate> CreateGeneratedContentItemUpdateAsync(
        Guid batchId,
        GenerationItemDto item,
        GenerationImageContentDto content,
        CancellationToken ct)
    {
        GenerationImageContentValidationResult? validationResult =
            GetValidationResultOrDefault(content);

        if (validationResult is null)
        {
            _logger.LogWarning(
                "Gallery rejected generated content for batch {BatchId} item {ItemId}",
                batchId,
                item.Id);

            return new GalleryCompletedItemUpdate(item, null, null);
        }

        string? expectedResultPath = _generationResultStorage.GetExpectedResultPathOrDefault(
            batchId,
            item.Id,
            validationResult.ContentType);

        if (expectedResultPath is null)
        {
            _logger.LogWarning(
                "Gallery could not resolve a managed result location for batch {BatchId} item {ItemId}",
                batchId,
                item.Id);

            return new GalleryCompletedItemUpdate(item, null, null);
        }

        try
        {
            await SaveContentAsync(batchId, item.Id, validationResult, ct)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            LogStorageFailure(ex, batchId, item.Id);

            return new GalleryCompletedItemUpdate(item, null, null);
        }
        catch (IOException ex)
        {
            LogStorageFailure(ex, batchId, item.Id);

            return new GalleryCompletedItemUpdate(item, null, null);
        }
        catch (NotSupportedException ex)
        {
            LogStorageFailure(ex, batchId, item.Id);

            return new GalleryCompletedItemUpdate(item, null, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogStorageFailure(ex, batchId, item.Id);

            return new GalleryCompletedItemUpdate(item, null, null);
        }

        string? trustedImagePath = _trustedImageFileService.GetTrustedImagePathOrDefault(
            expectedResultPath,
            item.ModelId);
        string? thumbnailPath = await SaveThumbnailAndGetPathOrDefaultAsync(
                batchId,
                item,
                trustedImagePath,
                ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "Gallery stored generated item {ItemId} from batch {BatchId}; thumbnail available: {HasThumbnail}",
            item.Id,
            batchId,
            thumbnailPath is not null);

        return new GalleryCompletedItemUpdate(item, trustedImagePath, thumbnailPath);
    }

    private string? GetLegacyTrustedImagePathOrDefault(GenerationItemDto item)
    {
        string? trustedImagePath = _trustedImageFileService.GetTrustedImagePathOrDefault(
            item.ImagePath,
            item.ModelId);

        return trustedImagePath;
    }

    private GenerationImageContentValidationResult? GetValidationResultOrDefault(GenerationImageContentDto content)
    {
        if (!_generationImageContentValidator.TryValidate(
            content,
            out GenerationImageContentValidationResult? validationResult)
            || validationResult is null)
        {
            return null;
        }

        return validationResult;
    }

    private Task SaveContentAsync(
        Guid batchId,
        Guid itemId,
        GenerationImageContentValidationResult validationResult,
        CancellationToken ct)
    {
        return _generationResultStorage.SaveAsync(batchId, itemId, validationResult, ct);
    }

    private async Task<string?> SaveThumbnailAndGetPathOrDefaultAsync(
        Guid batchId,
        GenerationItemDto item,
        string? trustedImagePath,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(trustedImagePath))
        {
            _logger.LogWarning(
                "Gallery skipped thumbnail generation for batch {BatchId} item {ItemId} because the result path is not trusted",
                batchId,
                item.Id);

            return null;
        }

        try
        {
            await _galleryThumbnailStorage
                .SaveAsync(batchId, item.Id, item.ModelId, trustedImagePath, ct)
                .ConfigureAwait(false);

            return _galleryThumbnailStorage.GetThumbnailPathOrDefault(batchId, item.Id, item.ModelId);
        }
        catch (ArgumentException ex)
        {
            LogThumbnailFallback(ex, batchId, item.Id);
            return null;
        }
        catch (InvalidDataException ex)
        {
            LogThumbnailFallback(ex, batchId, item.Id);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            LogThumbnailFallback(ex, batchId, item.Id);
            return null;
        }
        catch (IOException ex)
        {
            LogThumbnailFallback(ex, batchId, item.Id);
            return null;
        }
        catch (NotSupportedException ex)
        {
            LogThumbnailFallback(ex, batchId, item.Id);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogThumbnailFallback(ex, batchId, item.Id);
            return null;
        }
    }

    private void LogThumbnailFallback(
        Exception exception,
        Guid batchId,
        Guid itemId)
    {
        _logger.LogWarning(
            exception,
            "Gallery is continuing without a thumbnail for batch {BatchId} item {ItemId}",
            batchId,
            itemId);
    }

    private void LogStorageFailure(
        Exception exception,
        Guid batchId,
        Guid itemId)
    {
        _logger.LogWarning(
            exception,
            "Failed to save generation result for batch {BatchId} item {ItemId}",
            batchId,
            itemId);
    }
}
