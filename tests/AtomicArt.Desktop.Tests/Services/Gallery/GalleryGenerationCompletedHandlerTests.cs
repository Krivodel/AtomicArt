using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Gallery;

using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryGenerationCompletedHandlerTests
{
    private static readonly Guid CorrelationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BatchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ItemId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_WithImageContent_SavesApiBytesToResults()
    {
        string rootDirectory = CreateCleanDirectory(nameof(HandleAsync_WithImageContent_SavesApiBytesToResults));
        GenerationResultStorage storage = CreateStorage(rootDirectory);
        GalleryGenerationCompletedHandler handler = CreateHandler(
            storage,
            out List<IReadOnlyList<GalleryCompletedItemUpdate>> capturedUpdateBatches);
        byte[] validPngBytes = GenerationImageTestData.ValidPngBytes;
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Png,
            Convert.ToBase64String(validPngBytes));
        GenerationLifecycleEvent lifecycleEvent = CreateCompletedEvent(CreateItem(imageContent: content));

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        string resultPath = GetResultPath(storage, content.ContentType);
        byte[] savedBytes = await File.ReadAllBytesAsync(resultPath);
        savedBytes.Should().Equal(validPngBytes);
        capturedUpdateBatches.Should().ContainSingle();
        IReadOnlyList<GalleryCompletedItemUpdate> updates = capturedUpdateBatches[0];
        updates.Should().ContainSingle();
        updates[0].TrustedImagePath.Should().Be(resultPath);
    }

    [Fact]
    public async Task HandleAsync_WithImageContent_SavesCompletedGalleryMetadata()
    {
        string rootDirectory = CreateCleanDirectory(nameof(HandleAsync_WithImageContent_SavesCompletedGalleryMetadata));
        GenerationResultStorage storage = CreateStorage(rootDirectory);
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
        RecordingGalleryThumbnailStorage thumbnailStorage = new("thumbnail.png");
        GalleryGenerationCompletedHandler handler = new(
            new PassthroughTrustedImageFileService(),
            storage,
            thumbnailStorage,
            GenerationImageFormatRegistryTestFactory.CreateValidator(),
            statusRegistry,
            viewStateController,
            galleryStateService,
            NullLogger<GalleryGenerationCompletedHandler>.Instance);
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Png,
            Convert.ToBase64String(GenerationImageTestData.ValidPngBytes));

        await viewStateController.ApplyStartedAsync(CreateStartedEvent(), CancellationToken.None);
        await handler.HandleAsync(
            CreateCompletedEvent(CreateItem(imageContent: content)),
            CancellationToken.None);

        string resultPath = GetResultPath(storage, content.ContentType);
        galleryStateService.SavedItems.Should().ContainSingle();
        GalleryItemState savedItem = galleryStateService.SavedItems[0];
        savedItem.Status.Should().Be(GenerationItemStatus.Generated);
        savedItem.ImagePath.Should().Be(resultPath);
        savedItem.ThumbnailPath.Should().Be("thumbnail.png");
        savedItem.ModelId.Should().Be(ApiModelMetadataTestCatalog.NanoBanana2ModelId);
        GetArtFiles(rootDirectory).Should().ContainSingle();
        thumbnailStorage.FullImagePath.Should().Be(resultPath);
    }

    [Fact]
    public async Task HandleAsync_WithImageContentAndInvalidSignature_DoesNotUseLegacyPath()
    {
        string rootDirectory = CreateCleanDirectory(nameof(HandleAsync_WithImageContentAndInvalidSignature_DoesNotUseLegacyPath));
        GenerationResultStorage storage = CreateStorage(rootDirectory);
        GalleryGenerationCompletedHandler handler = CreateHandler(
            storage,
            out List<IReadOnlyList<GalleryCompletedItemUpdate>> capturedUpdateBatches);
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Png,
            Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03 }));
        GenerationLifecycleEvent lifecycleEvent = CreateCompletedEvent(CreateItem(
            imagePath: "legacy.png",
            imageContent: content));

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        capturedUpdateBatches.Should().ContainSingle();
        IReadOnlyList<GalleryCompletedItemUpdate> updates = capturedUpdateBatches[0];
        updates.Should().ContainSingle();
        updates[0].TrustedImagePath.Should().BeNull();
        GetArtFiles(rootDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithUnsupportedImageContent_DoesNotWriteFile()
    {
        string rootDirectory = CreateCleanDirectory(nameof(HandleAsync_WithUnsupportedImageContent_DoesNotWriteFile));
        GenerationResultStorage storage = CreateStorage(rootDirectory);
        GalleryGenerationCompletedHandler handler = CreateHandler(
            storage,
            out List<IReadOnlyList<GalleryCompletedItemUpdate>> capturedUpdateBatches);
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Gif,
            Convert.ToBase64String(GenerationImageTestData.ValidPngBytes));
        GenerationLifecycleEvent lifecycleEvent = CreateCompletedEvent(CreateItem(imageContent: content));

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        capturedUpdateBatches.Should().ContainSingle();
        IReadOnlyList<GalleryCompletedItemUpdate> updates = capturedUpdateBatches[0];
        updates.Should().ContainSingle();
        updates[0].TrustedImagePath.Should().BeNull();
        GetArtFiles(rootDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithLegacyPath_UsesCompatibilityPath()
    {
        string rootDirectory = CreateCleanDirectory(nameof(HandleAsync_WithLegacyPath_UsesCompatibilityPath));
        GenerationResultStorage storage = CreateStorage(rootDirectory);
        GalleryGenerationCompletedHandler handler = CreateHandler(
            storage,
            out List<IReadOnlyList<GalleryCompletedItemUpdate>> capturedUpdateBatches);
        GenerationLifecycleEvent lifecycleEvent = CreateCompletedEvent(CreateItem(imagePath: "legacy.png"));

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        capturedUpdateBatches.Should().ContainSingle();
        IReadOnlyList<GalleryCompletedItemUpdate> updates = capturedUpdateBatches[0];
        updates.Should().ContainSingle();
        updates[0].TrustedImagePath.Should().Be("legacy.png");
    }

    [Fact]
    public async Task HandleAsync_WithGeneratedItemWithoutContentOrTrustedPath_ReturnsEmptyImageState()
    {
        string rootDirectory = CreateCleanDirectory(
            nameof(HandleAsync_WithGeneratedItemWithoutContentOrTrustedPath_ReturnsEmptyImageState));
        GenerationResultStorage storage = CreateStorage(rootDirectory);
        GalleryGenerationCompletedHandler handler = CreateHandler(
            storage,
            out List<IReadOnlyList<GalleryCompletedItemUpdate>> capturedUpdateBatches,
            new RejectingTrustedImageFileService());
        GenerationLifecycleEvent lifecycleEvent = CreateCompletedEvent(CreateItem());

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        capturedUpdateBatches.Should().ContainSingle();
        IReadOnlyList<GalleryCompletedItemUpdate> updates = capturedUpdateBatches[0];
        updates.Should().ContainSingle();
        updates[0].TrustedImagePath.Should().BeNull();
        GetArtFiles(rootDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenStorageWriteFails_AppliesEmptyImageState()
    {
        string expectedResultPath = Path.Combine(
            CreateCleanDirectory(nameof(HandleAsync_WhenStorageWriteFails_AppliesEmptyImageState)),
            "existing.png");
        await File.WriteAllBytesAsync(expectedResultPath, GenerationImageTestData.ValidPngBytes);
        NotWritingGenerationResultStorage storage = new(expectedResultPath);
        GalleryGenerationCompletedHandler handler = CreateHandler(
            storage,
            out List<IReadOnlyList<GalleryCompletedItemUpdate>> capturedUpdateBatches,
            new ExistingFileTrustedImageFileService());
        GenerationImageContentDto content = new(
            GenerationImageContentTypes.Png,
            Convert.ToBase64String(GenerationImageTestData.ValidPngBytes));
        GenerationLifecycleEvent lifecycleEvent = CreateCompletedEvent(CreateItem(imageContent: content));

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        capturedUpdateBatches.Should().ContainSingle();
        IReadOnlyList<GalleryCompletedItemUpdate> updates = capturedUpdateBatches[0];
        updates.Should().ContainSingle();
        updates[0].TrustedImagePath.Should().BeNull();
    }

    private static GalleryGenerationCompletedHandler CreateHandler(
        IGenerationResultStorage storage,
        out List<IReadOnlyList<GalleryCompletedItemUpdate>> capturedUpdateBatches,
        ITrustedImageFileService? trustedImageFileService = null)
    {
        List<IReadOnlyList<GalleryCompletedItemUpdate>> updateBatches = [];
        Mock<IGalleryLifecycleViewState> viewStateMock = new();
        viewStateMock
            .Setup(viewState => viewState.ApplyCompletedAsync(
                CorrelationId,
                It.IsAny<IReadOnlyList<GalleryCompletedItemUpdate>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<GalleryCompletedItemUpdate>, CancellationToken>(
                (_, itemUpdates, _) => updateBatches.Add(itemUpdates))
            .Returns(Task.CompletedTask);
        viewStateMock
            .Setup(viewState => viewState.CreateStateSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        capturedUpdateBatches = updateBatches;
        IGenerationItemStatusDescriptorRegistry statusRegistry =
            GenerationItemStatusDescriptorRegistryTestFactory.Create();

        return new GalleryGenerationCompletedHandler(
            trustedImageFileService ?? new PassthroughTrustedImageFileService(),
            storage,
            new NullGalleryThumbnailStorage(),
            GenerationImageFormatRegistryTestFactory.CreateValidator(),
            statusRegistry,
            viewStateMock.Object,
            new RecordingGalleryStateService(),
            NullLogger<GalleryGenerationCompletedHandler>.Instance);
    }

    private static GenerationLifecycleEvent CreateCompletedEvent(GenerationItemDto item)
    {
        GenerationBatchDto batch = new(BatchId, [item]);

        return new GenerationLifecycleEvent(
            CorrelationId,
            GenerationLifecycleStatus.Completed,
            null,
            batch,
            null);
    }

    private static GenerationLifecycleEvent CreateStartedEvent()
    {
        GenerationStartSnapshot start = new(
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            "Prompt",
            GenerationAspectRatios.Auto,
            ApiModelMetadataTestCatalog.NanoBanana2Resolution,
            1,
            0,
            CreatedAtUtc);

        return new GenerationLifecycleEvent(
            CorrelationId,
            GenerationLifecycleStatus.Started,
            start,
            null,
            null);
    }

    private static GenerationItemDto CreateItem(
        string? imagePath = null,
        GenerationImageContentDto? imageContent = null)
    {
        return new GenerationItemDto(
            ItemId,
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            "Prompt",
            GenerationAspectRatios.Auto,
            ApiModelMetadataTestCatalog.NanoBanana2Resolution,
            CreatedAtUtc,
            GenerationItemStatus.Generated,
            imagePath,
            imageContent);
    }

    private static GenerationResultStorage CreateStorage(string rootDirectory)
    {
        return new GenerationResultStorage(
            new AtomicArtDataPathProvider(rootDirectory),
            GenerationImageFormatRegistryTestFactory.Create(),
            new GenerationImageFileNamePolicy(),
            NullLogger<GenerationResultStorage>.Instance);
    }

    private static string GetArtDirectory(string rootDirectory)
    {
        return new AtomicArtDataPathProvider(rootDirectory).ArtDirectory;
    }

    private static string[] GetArtFiles(string rootDirectory)
    {
        string artDirectory = GetArtDirectory(rootDirectory);

        return Directory.Exists(artDirectory)
            ? Directory.GetFiles(artDirectory)
            : [];
    }

    private static string GetResultPath(
        GenerationResultStorage storage,
        string contentType)
    {
        string? resultPath = storage.GetExpectedResultPathOrDefault(BatchId, ItemId, contentType);

        return resultPath ?? throw new InvalidOperationException("Generation result path is required.");
    }

    private sealed class NotWritingGenerationResultStorage : IGenerationResultStorage
    {
        private readonly string _expectedResultPath;

        public NotWritingGenerationResultStorage(string expectedResultPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedResultPath);

            _expectedResultPath = expectedResultPath;
        }

        public string GetExpectedResultPathOrDefault(
            Guid batchId,
            Guid itemId,
            string contentType)
        {
            return _expectedResultPath;
        }

        public Task SaveAsync(
            Guid batchId,
            Guid itemId,
            GenerationImageContentValidationResult content,
            CancellationToken ct)
        {
            throw new IOException("Test storage write failed.");
        }
    }

    private sealed class RecordingGalleryThumbnailStorage : IGalleryThumbnailStorage
    {
        private readonly string? _thumbnailPath;

        public string? FullImagePath { get; private set; }

        public RecordingGalleryThumbnailStorage(string? thumbnailPath)
        {
            _thumbnailPath = thumbnailPath;
        }

        public string? GetThumbnailPathOrDefault(
            Guid batchId,
            Guid itemId,
            string modelId)
        {
            return _thumbnailPath;
        }

        public Task SaveAsync(
            Guid batchId,
            Guid itemId,
            string modelId,
            string? fullImagePath,
            CancellationToken ct)
        {
            FullImagePath = fullImagePath;

            return Task.CompletedTask;
        }
    }

}
