using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal static class GalleryLifecycleTestFactory
{
    private static readonly DateTime RequestedAtUtc = new(
        2026,
        7,
        6,
        10,
        0,
        0,
        DateTimeKind.Utc);

    public static GenerationLifecycleEvent CreateStartedEvent(
        Guid correlationId,
        int generationCount)
    {
        return CreateStartedEvent(
            correlationId,
            RequestedAtUtc,
            generationCount,
            attachedImagesCount: 0);
    }

    public static GenerationLifecycleEvent CreateStartedEvent(
        Guid correlationId,
        DateTime requestedAtUtc,
        int generationCount,
        int attachedImagesCount)
    {
        GenerationStartSnapshot start = new(
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            "Prompt",
            GenerationAspectRatios.Auto,
            TestGenerationOutputMetadata.GeneratedImageResolution,
            generationCount,
            attachedImagesCount,
            requestedAtUtc);

        return new GenerationLifecycleEvent(
            correlationId,
            GenerationLifecycleStatus.Started,
            start,
            null,
            null);
    }

    public static GenerationLifecycleEvent CreateCompletedEvent(
        Guid correlationId,
        GenerationBatchDto batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new GenerationLifecycleEvent(
            correlationId,
            GenerationLifecycleStatus.Completed,
            null,
            batch,
            null);
    }

    public static GenerationLifecycleEvent CreateCompletedEvent(
        Guid correlationId,
        Guid batchId,
        GenerationItemDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return CreateCompletedEvent(
            correlationId,
            new GenerationBatchDto(batchId, new List<GenerationItemDto> { item }));
    }

    public static GenerationLifecycleEvent CreateStartFailedEvent(Guid correlationId)
    {
        return new GenerationLifecycleEvent(
            correlationId,
            GenerationLifecycleStatus.StartFailed,
            null,
            null,
            "Failed to start.");
    }

    public static GenerationLifecycleEvent CreateFailedEvent(Guid correlationId)
    {
        return new GenerationLifecycleEvent(
            correlationId,
            GenerationLifecycleStatus.Failed,
            null,
            null,
            "Generation failed.");
    }

    public static GalleryLifecycleViewStateController CreateViewStateController(
        IGenerationItemStatusDescriptorRegistry statusRegistry)
    {
        ArgumentNullException.ThrowIfNull(statusRegistry);

        GalleryItemsController itemsController = new(
            new PassthroughTrustedImageFileService(),
            statusRegistry);

        return new GalleryLifecycleViewStateController(
            new ImmediateUiThreadDispatcher(),
            new RecordingAnimatedGalleryOperations(),
            itemsController);
    }
}
