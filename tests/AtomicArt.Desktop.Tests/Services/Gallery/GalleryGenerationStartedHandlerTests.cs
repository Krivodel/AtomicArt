using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
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
        GalleryLifecycleViewStateController viewStateController =
            GalleryLifecycleTestFactory.CreateViewStateController(statusRegistry);
        RecordingGalleryStateService galleryStateService = new();
        GalleryGenerationStartedHandler handler = new(
            viewStateController,
            galleryStateService);

        GenerationLifecycleEvent startedEvent = GalleryLifecycleTestFactory.CreateStartedEvent(
            CorrelationId,
            RequestedAtUtc,
            generationCount: 2,
            attachedImagesCount: 1);

        await handler.HandleAsync(startedEvent, CancellationToken.None);

        galleryStateService.SavedItems.Should().HaveCount(2);
        galleryStateService.SavedItems.Should().OnlyContain(item =>
            item.Status == GenerationItemStatus.Generating
            && item.CorrelationId == CorrelationId);
        galleryStateService.SavedItems
            .Select(item => item.GenerationOrdinal)
            .Should()
            .Equal(0, 1);
    }

}
