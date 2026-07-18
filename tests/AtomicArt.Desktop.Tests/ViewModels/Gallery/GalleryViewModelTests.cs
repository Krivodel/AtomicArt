using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Generation;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.ViewModels.Gallery;
using AtomicArt.Tests.Common;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

using static AtomicArt.Desktop.Tests.Common.DesktopTestDirectories;

namespace AtomicArt.Desktop.Tests.ViewModels.Gallery;

public sealed class GalleryViewModelTests
{
    [Fact]
    public async Task DeleteOrCancel_WithGeneratedItem_RemovesItem()
    {
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel();
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(status: GenerationItemStatus.Generated)];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(item);

        viewModel.Items.Should().BeEmpty();
        viewModel.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOrCancelAsync_WithCompletedItem_DeletesFiles()
    {
        RecordingGalleryItemDeletionService deletionService = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            galleryItemDeletionService: deletionService);
        List<GenerationItemDto> items =
        [
            GalleryViewModelTestFactory.CreateItem(
                status: GenerationItemStatus.Generated,
                imagePath: "image.png")
        ];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];
        item.ThumbnailPath = "thumbnail.png";

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(item);

        deletionService.Requests.Should().ContainSingle();
        GalleryItemDeletionRequest request = deletionService.Requests[0];
        request.ItemId.Should().Be(item.Id);
        request.ModelId.Should().Be(item.ModelId);
        request.ImagePath.Should().Be("image.png");
        request.ThumbnailPath.Should().Be("thumbnail.png");
    }

    [Fact]
    public async Task DeleteOrCancelAsync_WithCompletedItem_SavesStateWithoutItem()
    {
        RecordingGalleryStateService galleryStateService = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            galleryStateService: galleryStateService);
        List<GenerationItemDto> items =
        [
            GalleryViewModelTestFactory.CreateItem(prompt: "Deleted", imagePath: "deleted.png"),
            GalleryViewModelTestFactory.CreateItem(prompt: "Remaining", imagePath: "remaining.png")
        ];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel deletedItem = viewModel.Items.Single(item => item.Prompt == "Deleted");

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(deletedItem);

        galleryStateService.SavedItems.Should().ContainSingle();
        galleryStateService.SavedItems[0].Prompt.Should().Be("Remaining");
        galleryStateService.SavedItems.Should().NotContain(item => item.Id == deletedItem.Id);
    }

    [Fact]
    public async Task DeleteOrCancelAsync_WhenFileDeleteFails_StillSavesStateWithoutItem()
    {
        Guid itemId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid batchId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
        string rootDirectory = CreateCleanDirectory(
            nameof(DeleteOrCancelAsync_WhenFileDeleteFails_StillSavesStateWithoutItem));
        AtomicArtDataPathProvider pathProvider = new(rootDirectory);
        Directory.CreateDirectory(pathProvider.ArtDirectory);
        GenerationImageFileNamePolicy fileNamePolicy = new();
        string imagePath = Path.Combine(
            pathProvider.ArtDirectory,
            fileNamePolicy.BuildFileName(batchId, itemId, ".png"));
        await File.WriteAllBytesAsync(imagePath, GenerationImageTestData.ValidPngBytes);
        GalleryItemDeletionService deletionService = new(
            new TrustedImageFileService(
                pathProvider,
                GenerationImageFormatRegistryTestFactory.Create(),
                NullLogger<TrustedImageFileService>.Instance),
            fileNamePolicy,
            NullLogger<GalleryItemDeletionService>.Instance);
        RecordingGalleryStateService galleryStateService = new();
        TestViewModelErrorHandler errorHandler = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            galleryStateService: galleryStateService,
            galleryItemDeletionService: deletionService,
            errorHandler: errorHandler);
        List<GenerationItemDto> items =
        [
            new(
                itemId,
                ApiModelMetadataTestCatalog.NanoBanana2ModelId,
                ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
                "Deleted",
                GenerationAspectRatios.Auto,
                ApiModelMetadataTestCatalog.NanoBanana2Resolution,
                DateTime.UtcNow,
                GenerationItemStatus.Generated,
                imagePath,
                null),
            GalleryViewModelTestFactory.CreateItem(prompt: "Remaining", imagePath: "remaining.png")
        ];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel deletedItem = viewModel.Items.Single(item => item.Id == itemId);
        
        await using FileStream lockedImage = new(
            imagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(deletedItem);

        File.Exists(imagePath).Should().BeTrue();
        viewModel.Items.Should().ContainSingle().Which.Prompt.Should().Be("Remaining");
        galleryStateService.SavedItems.Should().ContainSingle();
        galleryStateService.SavedItems[0].Prompt.Should().Be("Remaining");
        galleryStateService.SavedItems.Should().NotContain(item => item.Id == deletedItem.Id);
        viewModel.HasErrorMessage.Should().BeFalse();
        errorHandler.LogCallCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteOrCancelAsync_WithGeneratingItemWithoutFiles_RemovesItemAndSavesState()
    {
        RecordingGalleryStateService galleryStateService = new();
        RecordingGalleryItemDeletionService deletionService = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            galleryStateService: galleryStateService,
            galleryItemDeletionService: deletionService);
        List<GenerationItemDto> items =
        [
            GalleryViewModelTestFactory.CreateItem(status: GenerationItemStatus.Generating)
        ];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(item);

        viewModel.Items.Should().BeEmpty();
        galleryStateService.SavedItems.Should().BeEmpty();
        deletionService.Requests.Should().ContainSingle();
        deletionService.Requests[0].ImagePath.Should().BeNull();
        deletionService.Requests[0].ThumbnailPath.Should().BeNull();
    }

    [Fact]
    public async Task DeleteOrCancelAsync_WithNullItem_DoesNothing()
    {
        RecordingGalleryStateService galleryStateService = new();
        RecordingGalleryItemDeletionService deletionService = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            galleryStateService: galleryStateService,
            galleryItemDeletionService: deletionService);

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(null);

        deletionService.Requests.Should().BeEmpty();
        galleryStateService.SaveCallCount.Should().Be(0);
        viewModel.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteOrCancel_WithGeneratingItem_RemovesItem()
    {
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel();
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(status: GenerationItemStatus.Generating)];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(item);

        viewModel.Items.Should().BeEmpty();
        viewModel.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AddGeneratedItems_WithItems_UsesGenerateFrontOperation()
    {
        RecordingAnimatedGalleryOperations operations = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            animatedGalleryOperations: operations);
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(status: GenerationItemStatus.Generated)];

        viewModel.AddGeneratedItems(items, 0);

        operations.GenerateFrontCallCount.Should().Be(1);
        operations.LastGenerateFrontItems.Should().ContainSingle();
        operations.LastGenerateFrontItems[0].Should().BeSameAs(viewModel.Items[0]);
    }

    [Fact]
    public async Task RestoreStateAsync_WithItems_UsesRestoreSnapshotOperation()
    {
        RecordingAnimatedGalleryOperations operations = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            animatedGalleryOperations: operations);
        GalleryItemState state = CreateSavedGalleryItem();

        await viewModel.RestoreStateAsync([state], CancellationToken.None);

        viewModel.Items.Should().ContainSingle();
        operations.RestoreSnapshotCallCount.Should().Be(1);
        operations.LastRestoreSnapshotItems.Should().ContainSingle()
            .Which.Should().BeSameAs(viewModel.Items[0]);
    }

    [Fact]
    public async Task DeleteOrCancel_WithGeneratedItem_UsesRemoveOperation()
    {
        RecordingAnimatedGalleryOperations operations = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            animatedGalleryOperations: operations);
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(status: GenerationItemStatus.Generated)];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];

        await viewModel.DeleteOrCancelCommand.ExecuteAsync(item);

        operations.RemoveCallCount.Should().Be(1);
        operations.LastRemovedItemId.Should().Be(item.Id);
    }

    [Fact]
    public void OpenMetadata_WithItem_OpensMetadata()
    {
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel();
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(prompt: "Metadata prompt")];
        viewModel.AddGeneratedItems(items, 2);
        GenerationItemViewModel item = viewModel.Items[0];

        viewModel.OpenMetadataCommand.Execute(item);

        viewModel.IsMetadataOpen.Should().BeTrue();
        viewModel.SelectedMetadata.Should().NotBeNull();
        viewModel.SelectedMetadata?.Prompt.Should().Be("Metadata prompt");
    }

    [Fact]
    public void CloseOverlay_WithOpenMetadata_ClosesOverlay()
    {
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel();
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(imagePath: "image.png")];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];
        viewModel.OpenMetadataCommand.Execute(item);

        viewModel.CloseOverlayCommand.Execute(null);

        viewModel.IsMetadataOpen.Should().BeFalse();
    }

    [Fact]
    public async Task RevealInFolderAsync_WhenServiceThrows_SetsErrorMessage()
    {
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            fileRevealService: new ThrowingFileRevealService());
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(imagePath: "image.png")];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];

        await viewModel.RevealInFolderCommand.ExecuteAsync(item);

        viewModel.HasErrorMessage.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task RevealInFolderCommand_WhenImagePathExists_CallsFileRevealService()
    {
        SuccessfulFileRevealService fileRevealService = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            fileRevealService: fileRevealService);
        List<GenerationItemDto> items = [GalleryViewModelTestFactory.CreateItem(imagePath: "image.png")];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel item = viewModel.Items[0];

        await viewModel.RevealInFolderCommand.ExecuteAsync(item);

        fileRevealService.CallCount.Should().Be(1);
        fileRevealService.RevealedPath.Should().Be("image.png");
        fileRevealService.RevealedModelId.Should().Be(item.ModelId);
    }

    [Fact]
    public async Task OpenViewerCommand_WithGeneratedImage_CallsImageViewerServiceWithFullImages()
    {
        RecordingImageViewerService imageViewerService = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            imageViewerService: imageViewerService);
        List<GenerationItemDto> items =
        [
            GalleryViewModelTestFactory.CreateItem(prompt: "First", imagePath: "first.png"),
            GalleryViewModelTestFactory.CreateItem(prompt: "Second", imagePath: "second.png"),
            GalleryViewModelTestFactory.CreateItem(
                prompt: "Empty",
                status: GenerationItemStatus.Generated,
                imagePath: null)
        ];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel selectedItem = viewModel.Items.Single(item => item.Prompt == "Second");

        await viewModel.OpenViewerCommand.ExecuteAsync(selectedItem);

        imageViewerService.OpenCallCount.Should().Be(1);
        imageViewerService.LastRequest.Should().NotBeNull();
        GalleryImageViewerRequest? request = imageViewerService.LastRequest;
        request?.SelectedItemId.Should().Be(selectedItem.Id);
        request?.ItemsSource.GetItems().Select(GetFileSourcePath).Should().BeEquivalentTo("first.png", "second.png");
    }

    [Fact]
    public async Task OpenViewerCommand_WhileViewerIsAlreadyOpen_StartsAnotherViewer()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(5));
        BlockingImageViewerService imageViewerService = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            imageViewerService: imageViewerService);
        List<GenerationItemDto> items =
        [
            GalleryViewModelTestFactory.CreateItem(prompt: "First", imagePath: "first.png"),
            GalleryViewModelTestFactory.CreateItem(prompt: "Second", imagePath: "second.png")
        ];
        viewModel.AddGeneratedItems(items, 0);
        GenerationItemViewModel firstItem = viewModel.Items.Single(item => item.Prompt == "First");
        GenerationItemViewModel secondItem = viewModel.Items.Single(item => item.Prompt == "Second");

        Task firstOpen = viewModel.OpenViewerCommand.ExecuteAsync(firstItem);
        await imageViewerService.WaitForOpenCallAsync(cancellation.Token);
        Task secondOpen = viewModel.OpenViewerCommand.ExecuteAsync(secondItem);
        await imageViewerService.WaitForOpenCallAsync(cancellation.Token);
        imageViewerService.Release();
        await Task.WhenAll(firstOpen, secondOpen);

        imageViewerService.OpenCallCount.Should().Be(2);
    }

    [Fact]
    public void OnGenerationStarted_WithSingleItem_InsertsGeneratingPlaceholderAtStart()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 1));

        viewModel.Items.Should().ContainSingle();
        viewModel.Items[0].IsGenerating.Should().BeTrue();
        viewModel.Items[0].CorrelationId.Should().Be(correlationId);
        viewModel.Items[0].GenerationOrdinal.Should().Be(0);
        viewModel.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void OnGenerationStarted_WithSingleItem_UsesGenerateFrontOperation()
    {
        RecordingAnimatedGalleryOperations operations = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            lifecycleEventHub: lifecycleEventHub,
            animatedGalleryOperations: operations);
        Guid correlationId = Guid.NewGuid();

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 1));

        operations.GenerateFrontCallCount.Should().Be(1);
        operations.LastGenerateFrontItems.Should().ContainSingle();
        operations.LastGenerateFrontItems[0].Should().BeSameAs(viewModel.Items[0]);
    }

    [Fact]
    public void OnGenerationStarted_WhenEventPublished_UsesUiThreadDispatcher()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        ImmediateUiThreadDispatcher uiThreadDispatcher = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            uiThreadDispatcher: uiThreadDispatcher,
            lifecycleEventHub: lifecycleEventHub);
        int callCountBeforeEvent = uiThreadDispatcher.CallCount;

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(Guid.NewGuid(), generationCount: 1));

        uiThreadDispatcher.CallCount.Should().BeGreaterThan(callCountBeforeEvent);
        viewModel.Items.Should().ContainSingle();
    }

    [Fact]
    public void OnGenerationStarted_WithMultipleItems_PreservesPlaceholderOrder()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 3));

        viewModel.Items.Should().HaveCount(3);
        viewModel.Items.Select(item => item.GenerationOrdinal).Should().Equal(0, 1, 2);
        viewModel.Items.Select(item => item.CorrelationId).Should().OnlyContain(id => id == correlationId);
    }

    [Fact]
    public void OnGenerationCompleted_WithMatchingCorrelationId_ReplacesPlaceholders()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 1));
        GenerationItemViewModel placeholder = viewModel.Items[0];
        GenerationItemDto resultItem = GalleryViewModelTestFactory.CreateItem(
            prompt: "Finished",
            status: GenerationItemStatus.Generated,
            imagePath: "image.png");
        GenerationBatchDto batch = new(
            Guid.NewGuid(),
            [resultItem]);

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateCompletedEvent(correlationId, batch));

        viewModel.Items[0].Should().BeSameAs(placeholder);
        viewModel.Items[0].IsGenerated.Should().BeTrue();
        viewModel.Items[0].Prompt.Should().Be("Finished");
        viewModel.Items[0].ImagePath.Should().Be("image.png");
        viewModel.Items[0].CorrelationId.Should().BeNull();
    }

    [Fact]
    public void OnGenerationCompleted_WithUntrustedImagePath_KeepsSafeDisplayState()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            trustedImageFileService: new RejectingTrustedImageFileService(),
            lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 1));
        GenerationBatchDto batch = new(
            Guid.NewGuid(),
            [GalleryViewModelTestFactory.CreateItem(imagePath: "unsafe.png")]);

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateCompletedEvent(correlationId, batch));

        viewModel.Items[0].ImagePath.Should().BeNull();
        viewModel.Items[0].HasDisplayImagePath.Should().BeFalse();
    }

    [Fact]
    public void Completed_WhenGeneratedItemHasNoImagePath_UsesEmptyImageState()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 1));
        GenerationBatchDto batch = new(
            Guid.NewGuid(),
            [GalleryViewModelTestFactory.CreateItem(imagePath: null)]);

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateCompletedEvent(correlationId, batch));

        viewModel.Items[0].ImagePath.Should().BeNull();
        viewModel.Items[0].HasDisplayImagePath.Should().BeFalse();
    }

    [Fact]
    public void Completed_WhenGenerationFailed_KeepsFailedImageState()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 1));
        GenerationBatchDto batch = new(
            Guid.NewGuid(),
            [
                GalleryViewModelTestFactory.CreateItem(status: GenerationItemStatus.Failed, imagePath: null)
            ]);

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateCompletedEvent(correlationId, batch));

        viewModel.Items[0].IsFailed.Should().BeTrue();
        viewModel.Items[0].ImagePath.Should().BeNull();
    }

    [Fact]
    public void OnGenerationFailed_WithMatchingCorrelationId_MarksPlaceholdersAsFailed()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 2));

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateFailedEvent(correlationId));

        viewModel.Items.Should().OnlyContain(item => item.IsFailed);
        viewModel.Items.Should().OnlyContain(item => !item.IsGenerating);
    }

    [Fact]
    public void OnGenerationStartFailed_WithMatchingCorrelationId_RemovesPlaceholders()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid correlationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 2));

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartFailedEvent(correlationId));

        viewModel.Items.Should().BeEmpty();
        viewModel.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void OnGenerationStartFailed_WithMatchingCorrelationId_UsesMixedMutationOperation()
    {
        RecordingAnimatedGalleryOperations operations = new();
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            lifecycleEventHub: lifecycleEventHub,
            animatedGalleryOperations: operations);
        Guid correlationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(correlationId, generationCount: 2));

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartFailedEvent(correlationId));

        operations.MixedMutationCallCount.Should().Be(1);
        operations.LastMixedMutationItems.Should().BeEmpty();
    }

    [Fact]
    public void OnGenerationFailed_WithDifferentCorrelationId_DoesNotChangeOtherRun()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        Guid firstCorrelationId = Guid.NewGuid();
        Guid secondCorrelationId = Guid.NewGuid();
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(firstCorrelationId, generationCount: 1));
        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateStartedEvent(secondCorrelationId, generationCount: 1));

        lifecycleEventHub.Publish(GalleryViewModelTestFactory.CreateFailedEvent(firstCorrelationId));

        GenerationItemViewModel failedItem = viewModel.Items.Single(item => item.CorrelationId == firstCorrelationId);
        GenerationItemViewModel activeItem = viewModel.Items.Single(item => item.CorrelationId == secondCorrelationId);
        failedItem.IsFailed.Should().BeTrue();
        activeItem.IsGenerating.Should().BeTrue();
    }

    [Fact]
    public async Task OnGenerationFailed_AfterDispatcherApiFailure_KeepsFailedPlaceholder()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            lifecycleEventHub: lifecycleEventHub);
        GenerationRunDispatcher dispatcher = new(
            new GenerationConcurrencyLimiter(),
            new ThrowingImageGenerationApiClient(),
            new NanoBanana2GenerationLifecyclePublisher(lifecycleEventHub),
            new NullGenerationResultStorage(),
            TestGenerationActivityTrackerFactory.Create(),
            NullLogger<GenerationRunDispatcher>.Instance);

        await dispatcher.EnqueueAsync(CreateRunRequest(), CancellationToken.None);

        await AsyncTestWaiter.WaitForConditionAsync(
            () => viewModel.Items is [{ IsFailed: true }],
            CancellationToken.None);
        viewModel.Items.Should().ContainSingle();
        viewModel.Items[0].IsFailed.Should().BeTrue();
        viewModel.IsEmpty.Should().BeFalse();
    }

    private static GenerationRunRequest CreateRunRequest()
    {
        ImageGenerationRequestDto request = new(
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            "Prompt",
            "Авто",
            ApiModelMetadataTestCatalog.NanoBanana2Resolution,
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().Temperature.Default,
            1,
            []);
        GenerationStartSnapshot startSnapshot = new(
            request.ModelId,
            ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            request.GenerationCount,
            request.AttachedImages.Count,
            new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc));

        return new GenerationRunRequest(
            request,
            startSnapshot,
            TestGenerationCredentials.ProviderCredential);
    }

    private static GalleryItemState CreateSavedGalleryItem()
    {
        return new GalleryItemState
        {
            Id = Guid.NewGuid(),
            ModelId = ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            ModelDisplayName = ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            Prompt = "Saved prompt",
            AspectRatio = GenerationAspectRatios.Auto,
            Resolution = ApiModelMetadataTestCatalog.NanoBanana2Resolution,
            CreatedAtUtc = DateTime.UtcNow,
            Status = GenerationItemStatus.Generated,
            ImagePath = "image.png"
        };
    }

    private static string GetFileSourcePath(GalleryImageViewerItem item)
    {
        item.Source.Should().BeOfType<GalleryFileImageViewerSource>();

        return ((GalleryFileImageViewerSource)item.Source).ImagePath;
    }

    private sealed class RecordingGalleryItemDeletionService : IGalleryItemDeletionService
    {
        private readonly List<GalleryItemDeletionRequest> _requests = [];

        public IReadOnlyList<GalleryItemDeletionRequest> Requests => _requests;

        public Task DeleteFilesAsync(GalleryItemDeletionRequest request, CancellationToken ct)
        {
            _requests.Add(request);

            return Task.CompletedTask;
        }
    }
}
