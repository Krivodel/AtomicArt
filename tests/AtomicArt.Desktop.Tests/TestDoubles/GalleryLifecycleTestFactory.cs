using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.TestDoubles;

internal static class GalleryLifecycleTestFactory
{
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
