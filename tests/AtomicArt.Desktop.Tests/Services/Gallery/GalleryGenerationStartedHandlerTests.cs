using Avalonia.Threading;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryGenerationStartedHandlerTests : DesktopControlTestBase
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
            galleryStateService,
            Mock.Of<IGenerationReferenceStore>());

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

    [Fact]
    public async Task HandleAsync_FromBackgroundThread_AnimatesAndPersistsPlaceholder()
    {
        await DispatchAsync(async () =>
        {
            Mock<IAnimatedGalleryOperations> operations = new();
            operations
                .Setup(gallery => gallery.GenerateFrontAsync(
                    It.IsAny<IReadOnlyList<object>>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Dispatcher.UIThread.VerifyAccess();
                    return Task.CompletedTask;
                });
            GalleryLifecycleViewStateController viewState = GalleryLifecycleTestFactory.CreateViewStateController(
                GenerationItemStatusDescriptorRegistryTestFactory.Create(),
                new AvaloniaUiThreadDispatcher(),
                operations.Object);
            RecordingGalleryStateService galleryState = new();
            GalleryGenerationStartedHandler handler = new(
                viewState, galleryState, Mock.Of<IGenerationReferenceStore>());
            GenerationLifecycleEvent started = GalleryLifecycleTestFactory.CreateStartedEvent(CorrelationId, 1);

            await Task.Run(() => handler.HandleAsync(started, CancellationToken.None));

            operations.Verify(gallery => gallery.GenerateFrontAsync(
                It.IsAny<IReadOnlyList<object>>(), CancellationToken.None), Times.Once);
            galleryState.SavedItems.Should().ContainSingle()
                .Which.Status.Should().Be(GenerationItemStatus.Generating);
        });
    }
}
