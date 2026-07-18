using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Gallery.Thumbnails;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

internal static class GalleryViewModelTestFactory
{
    public static GalleryViewModel CreateViewModel(
        IFileRevealService? fileRevealService = null,
        IImageViewerService? imageViewerService = null,
        ITrustedImageFileService? trustedImageFileService = null,
        IGenerationResultStorage? generationResultStorage = null,
        IGenerationImageContentValidator? generationImageContentValidator = null,
        IGenerationItemStatusDescriptorRegistry? statusDescriptorRegistry = null,
        IUiThreadDispatcher? uiThreadDispatcher = null,
        IGenerationLifecycleEventHub? lifecycleEventHub = null,
        IAnimatedGalleryOperations? animatedGalleryOperations = null,
        IViewModelErrorHandler? errorHandler = null,
        IGalleryStateService? galleryStateService = null,
        IGalleryItemDeletionService? galleryItemDeletionService = null,
        IGalleryThumbnailStorage? galleryThumbnailStorage = null)
    {
        IFileRevealService revealService =
            fileRevealService ?? new SuccessfulFileRevealService();
        IImageViewerService viewerService =
            imageViewerService ?? new NullImageViewerService();
        ITrustedImageFileService trustedService =
            trustedImageFileService ?? new PassthroughTrustedImageFileService();
        IGenerationResultStorage resultStorage =
            generationResultStorage ?? new GenerationResultStorage(
                new AtomicArtDataPathProvider(CreateTempRootDirectory()),
                GenerationImageFormatRegistryTestFactory.Create(),
                new GenerationImageFileNamePolicy(),
                NullLogger<GenerationResultStorage>.Instance);
        IGenerationImageContentValidator contentValidator =
            generationImageContentValidator ?? GenerationImageFormatRegistryTestFactory.CreateValidator();
        IGenerationItemStatusDescriptorRegistry statusRegistry =
            statusDescriptorRegistry ?? GenerationItemStatusDescriptorRegistryTestFactory.Create();
        IUiThreadDispatcher uiThreadService =
            uiThreadDispatcher ?? new ImmediateUiThreadDispatcher();
        IGenerationLifecycleEventHub generationLifecycleEventHub =
            lifecycleEventHub ?? new TestGenerationLifecycleEventHub();
        IAnimatedGalleryOperations galleryOperations =
            animatedGalleryOperations ?? new RecordingAnimatedGalleryOperations();
        IViewModelErrorHandler viewModelErrorHandler =
            errorHandler ?? new TestViewModelErrorHandler();
        IGalleryStateService galleryState =
            galleryStateService ?? new RecordingGalleryStateService();
        IGalleryItemDeletionService deletionService =
            galleryItemDeletionService ?? new NullGalleryItemDeletionService();
        IGalleryThumbnailStorage thumbnailStorage =
            galleryThumbnailStorage ?? new NullGalleryThumbnailStorage();
        GalleryItemsController itemsController = new(trustedService, statusRegistry);
        GalleryLifecycleViewStateController viewStateController = new(
            uiThreadService,
            galleryOperations,
            itemsController);
        IGalleryLifecycleEventHandler[] lifecycleEventHandlers =
        [
            new GalleryGenerationCompletedHandler(
                trustedService,
                resultStorage,
                thumbnailStorage,
                contentValidator,
                statusRegistry,
                viewStateController,
                galleryState,
                NullLogger<GalleryGenerationCompletedHandler>.Instance),
            new GalleryGenerationFailedHandler(viewStateController),
            new GalleryGenerationStartedHandler(viewStateController, galleryState),
            new GalleryGenerationStartFailedHandler(viewStateController)
        ];
        GalleryLifecycleController lifecycleController = new(
            generationLifecycleEventHub,
            viewStateController,
            viewModelErrorHandler,
            TestGenerationActivityTrackerFactory.Create(),
            lifecycleEventHandlers,
            NullLogger<GalleryLifecycleController>.Instance);

        return new GalleryViewModel(
            revealService,
            viewerService,
            deletionService,
            galleryState,
            viewStateController,
            itemsController,
            lifecycleController,
            viewModelErrorHandler,
            new GenerationPriceFormatter(),
            new GenerationDurationFormatter());
    }

    public static GenerationLifecycleEvent CreateStartedEvent(Guid correlationId, int generationCount)
    {
        GenerationStartSnapshot start = new(
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            "Prompt",
            GenerationAspectRatios.Auto,
            ApiModelMetadataTestCatalog.NanoBanana2Resolution,
            generationCount,
            0,
            DateTime.UtcNow);

        return new GenerationLifecycleEvent(
            correlationId,
            GenerationLifecycleStatus.Started,
            start,
            null,
            null);
    }

    private static string CreateTempRootDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "AtomicArtDesktopTests",
            nameof(GalleryViewModelTestFactory),
            Guid.NewGuid().ToString("N"));
    }

    private sealed class NullGalleryItemDeletionService : IGalleryItemDeletionService
    {
        public Task DeleteFilesAsync(GalleryItemDeletionRequest request, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NullImageViewerService : IImageViewerService
    {
        public Task OpenAsync(GalleryImageViewerRequest request, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }

    public static GenerationLifecycleEvent CreateCompletedEvent(
        Guid correlationId,
        GenerationBatchDto batch)
    {
        return new GenerationLifecycleEvent(
            correlationId,
            GenerationLifecycleStatus.Completed,
            null,
            batch,
            null);
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

    public static GenerationItemDto CreateItem(
        string prompt = "Prompt",
        GenerationItemStatus status = GenerationItemStatus.Generated,
        string? imagePath = null)
    {
        return new GenerationItemDto(
            Guid.NewGuid(),
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            prompt,
            GenerationAspectRatios.Auto,
            ApiModelMetadataTestCatalog.NanoBanana2Resolution,
            DateTime.UtcNow,
            status,
            imagePath,
            null);
    }
}
