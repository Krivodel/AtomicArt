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
    [Theory]
    [InlineData(GenerationItemStatus.Generated)]
    [InlineData(GenerationItemStatus.Generating)]
    public async Task DeleteOrCancel_WithRemovableItem_RemovesItem(
        GenerationItemStatus status)
    {
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel();
        List<GenerationItemDto> items =
        [
            GalleryViewModelTestFactory.CreateItem(status: status)
        ];
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
                TestGenerationOutputMetadata.GeneratedImageResolution,
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
        GalleryItemState state = GalleryItemStateTestFactory.CreateGenerated();

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
        using GalleryLifecycleTestContext context = new();

        Guid correlationId = context.Start(1);

        context.ViewModel.Items.Should().ContainSingle();
        context.ViewModel.Items[0].IsGenerating.Should().BeTrue();
        context.ViewModel.Items[0].CorrelationId.Should().Be(correlationId);
        context.ViewModel.Items[0].GenerationOrdinal.Should().Be(0);
        context.ViewModel.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void OnGenerationStarted_WithSingleItem_UsesGenerateFrontOperation()
    {
        RecordingAnimatedGalleryOperations operations = new();
        using GalleryLifecycleTestContext context = new(
            animatedGalleryOperations: operations);

        context.Start(1);

        operations.GenerateFrontCallCount.Should().Be(1);
        operations.LastGenerateFrontItems.Should().ContainSingle();
        operations.LastGenerateFrontItems[0].Should().BeSameAs(context.ViewModel.Items[0]);
    }

    [Fact]
    public void OnGenerationStarted_WhenEventPublished_UsesUiThreadDispatcher()
    {
        ImmediateUiThreadDispatcher uiThreadDispatcher = new();
        using GalleryLifecycleTestContext context = new(
            uiThreadDispatcher: uiThreadDispatcher);
        int callCountBeforeEvent = uiThreadDispatcher.CallCount;

        context.Start(1);

        uiThreadDispatcher.CallCount.Should().BeGreaterThan(callCountBeforeEvent);
        context.ViewModel.Items.Should().ContainSingle();
    }

    [Fact]
    public void OnGenerationStarted_WithMultipleItems_PreservesPlaceholderOrder()
    {
        using GalleryLifecycleTestContext context = new();

        Guid correlationId = context.Start(3);

        context.ViewModel.Items.Should().HaveCount(3);
        context.ViewModel.Items.Select(item => item.GenerationOrdinal).Should().Equal(0, 1, 2);
        context.ViewModel.Items.Select(item => item.CorrelationId).Should().OnlyContain(id => id == correlationId);
    }

    [Fact]
    public void OnGenerationCompleted_WithMatchingCorrelationId_ReplacesPlaceholders()
    {
        using GalleryLifecycleTestContext context = new();
        Guid correlationId = context.Start(1);
        GenerationItemViewModel placeholder = context.ViewModel.Items[0];
        GenerationItemDto resultItem = GalleryViewModelTestFactory.CreateItem(
            prompt: "Finished",
            status: GenerationItemStatus.Generated,
            imagePath: "image.png");

        context.Complete(correlationId, resultItem);

        context.ViewModel.Items[0].Should().BeSameAs(placeholder);
        context.ViewModel.Items[0].IsGenerated.Should().BeTrue();
        context.ViewModel.Items[0].Prompt.Should().Be("Finished");
        context.ViewModel.Items[0].ImagePath.Should().Be("image.png");
        context.ViewModel.Items[0].CorrelationId.Should().BeNull();
    }

    [Fact]
    public void OnGenerationCompleted_WithUntrustedImagePath_KeepsSafeDisplayState()
    {
        using GalleryLifecycleTestContext context = new(
            new RejectingTrustedImageFileService());
        Guid correlationId = context.Start(1);
        GenerationItemDto resultItem = GalleryViewModelTestFactory.CreateItem(
            imagePath: "unsafe.png");

        context.Complete(correlationId, resultItem);

        context.ViewModel.Items[0].ImagePath.Should().BeNull();
        context.ViewModel.Items[0].HasDisplayImagePath.Should().BeFalse();
    }

    [Fact]
    public void Completed_WhenGeneratedItemHasNoImagePath_UsesEmptyImageState()
    {
        using GalleryLifecycleTestContext context = new();
        Guid correlationId = context.Start(1);
        GenerationItemDto resultItem = GalleryViewModelTestFactory.CreateItem(
            imagePath: null);

        context.Complete(correlationId, resultItem);

        context.ViewModel.Items[0].ImagePath.Should().BeNull();
        context.ViewModel.Items[0].HasDisplayImagePath.Should().BeFalse();
    }

    [Fact]
    public void Completed_WhenGenerationFailed_KeepsFailedImageState()
    {
        using GalleryLifecycleTestContext context = new();
        Guid correlationId = context.Start(1);
        GenerationItemDto resultItem = GalleryViewModelTestFactory.CreateItem(
            status: GenerationItemStatus.Failed,
            imagePath: null);

        context.Complete(correlationId, resultItem);

        context.ViewModel.Items[0].IsFailed.Should().BeTrue();
        context.ViewModel.Items[0].ImagePath.Should().BeNull();
    }

    [Fact]
    public void OnGenerationFailed_WithMatchingCorrelationId_MarksPlaceholdersAsFailed()
    {
        using GalleryLifecycleTestContext context = new();

        Guid correlationId = context.Start(2);

        context.PublishFailure(correlationId);

        context.ViewModel.Items.Should().OnlyContain(item => item.IsFailed);
        context.ViewModel.Items.Should().OnlyContain(item => !item.IsGenerating);
    }

    [Fact]
    public void OnGenerationStartFailed_WithMatchingCorrelationId_RemovesPlaceholders()
    {
        using GalleryLifecycleTestContext context = new();

        Guid correlationId = context.Start(2);

        context.PublishStartFailure(correlationId);

        context.ViewModel.Items.Should().BeEmpty();
        context.ViewModel.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void OnGenerationStartFailed_WithMatchingCorrelationId_UsesMixedMutationOperation()
    {
        RecordingAnimatedGalleryOperations operations = new();
        using GalleryLifecycleTestContext context = new(
            animatedGalleryOperations: operations);

        Guid correlationId = context.Start(2);

        context.PublishStartFailure(correlationId);

        operations.MixedMutationCallCount.Should().Be(1);
        operations.LastMixedMutationItems.Should().BeEmpty();
    }

    [Fact]
    public void OnGenerationFailed_WithDifferentCorrelationId_DoesNotChangeOtherRun()
    {
        using GalleryLifecycleTestContext context = new();

        Guid firstCorrelationId = context.Start(1);
        Guid secondCorrelationId = context.Start(1);

        context.PublishFailure(firstCorrelationId);

        GenerationItemViewModel failedItem = context.ViewModel.Items.Single(item => item.CorrelationId == firstCorrelationId);
        GenerationItemViewModel activeItem = context.ViewModel.Items.Single(item => item.CorrelationId == secondCorrelationId);
        failedItem.IsFailed.Should().BeTrue();
        activeItem.IsGenerating.Should().BeTrue();
    }

    [Fact]
    public async Task OnGenerationFailed_AfterDispatcherApiFailure_KeepsFailedPlaceholder()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        using GalleryViewModel viewModel = GalleryViewModelTestFactory.CreateViewModel(
            lifecycleEventHub: lifecycleEventHub);
        GenerationRunDispatcher dispatcher = GenerationRunDispatcherTestFactory.Create(
            new ThrowingImageGenerationApiClient(),
            lifecycleEventHub);

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
        ImageGenerationRequestDto request = ImageGenerationRequestDtoTestFactory.Create(
            aspectRatio: "Авто",
            temperature: ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().Temperature.Default);
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

    private static string GetFileSourcePath(GalleryImageViewerItem item)
    {
        item.Source.Should().BeOfType<GalleryFileImageViewerSource>();

        return ((GalleryFileImageViewerSource)item.Source).ImagePath;
    }

    private sealed class GalleryLifecycleTestContext : IDisposable
    {
        public GalleryViewModel ViewModel { get; }

        private readonly TestGenerationLifecycleEventHub _lifecycleEventHub;

        public GalleryLifecycleTestContext(
            ITrustedImageFileService? trustedImageFileService = null,
            IAnimatedGalleryOperations? animatedGalleryOperations = null,
            IUiThreadDispatcher? uiThreadDispatcher = null)
        {
            _lifecycleEventHub = new TestGenerationLifecycleEventHub();
            ViewModel = GalleryViewModelTestFactory.CreateViewModel(
                trustedImageFileService: trustedImageFileService,
                lifecycleEventHub: _lifecycleEventHub,
                animatedGalleryOperations: animatedGalleryOperations,
                uiThreadDispatcher: uiThreadDispatcher);
        }

        public Guid Start(int generationCount)
        {
            Guid correlationId = Guid.NewGuid();
            _lifecycleEventHub.Publish(
                GalleryViewModelTestFactory.CreateStartedEvent(
                    correlationId,
                    generationCount));

            return correlationId;
        }

        public void Complete(Guid correlationId, GenerationItemDto item)
        {
            GenerationBatchDto batch = new(Guid.NewGuid(), [item]);
            GenerationLifecycleEvent completedEvent =
                GalleryViewModelTestFactory.CreateCompletedEvent(
                    correlationId,
                    batch);

            _lifecycleEventHub.Publish(completedEvent);
        }

        public void PublishFailure(Guid correlationId)
        {
            _lifecycleEventHub.Publish(
                GalleryViewModelTestFactory.CreateFailedEvent(correlationId));
        }

        public void PublishStartFailure(Guid correlationId)
        {
            _lifecycleEventHub.Publish(
                GalleryViewModelTestFactory.CreateStartFailedEvent(correlationId));
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
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
