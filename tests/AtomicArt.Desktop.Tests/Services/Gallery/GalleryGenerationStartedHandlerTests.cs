using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryGenerationStartedHandlerTests
{
    private static readonly Guid CorrelationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime RequestedAtUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_WithSnapshot_PersistsPlaceholders()
    {
        IGenerationItemStatusDescriptorRegistry statusRegistry =
            GenerationItemStatusDescriptorRegistryTestFactory.Create();
        GalleryItemsController itemsController = new(
            new PassthroughTrustedImageFileService(),
            statusRegistry);
        GalleryLifecycleViewStateController viewStateController = new(
            new ImmediateUiThreadDispatcher(),
            new RecordingAnimatedGalleryOperations(),
            itemsController);
        RecordingGalleryStateService galleryStateService = new();
        GalleryGenerationStartedHandler handler = new(
            viewStateController,
            galleryStateService);

        await handler.HandleAsync(CreateStartedEvent(), CancellationToken.None);

        galleryStateService.SavedItems.Should().HaveCount(2);
        galleryStateService.SavedItems.Should().OnlyContain(item =>
            item.Status == GenerationItemStatus.Generating
            && item.CorrelationId == CorrelationId);
        galleryStateService.SavedItems
            .Select(item => item.GenerationOrdinal)
            .Should()
            .Equal(0, 1);
    }

    private static GenerationLifecycleEvent CreateStartedEvent()
    {
        GenerationStartSnapshot start = new(
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            "Prompt",
            GenerationAspectRatios.Auto,
            TestGenerationOutputMetadata.GeneratedImageResolution,
            2,
            1,
            RequestedAtUtc);

        return new GenerationLifecycleEvent(
            CorrelationId,
            GenerationLifecycleStatus.Started,
            start,
            null,
            null);
    }

}
