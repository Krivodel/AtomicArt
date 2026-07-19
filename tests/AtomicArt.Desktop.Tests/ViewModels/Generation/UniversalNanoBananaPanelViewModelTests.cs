using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Generation;
using AtomicArt.Desktop.Views.Generation;
using AtomicArt.Infrastructure.Generation;
using AtomicArt.Tests.Common;
using static AtomicArt.Desktop.Tests.ViewModels.Generation.UniversalNanoBananaPanelViewModelTestHelper;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

public sealed class UniversalNanoBananaPanelViewModelTests
{
    private const int PromptDelayedSaveSettleMilliseconds = 500;
    private static readonly byte[] PngBytes = [.. GenerationImageFileSignatures.Png, 0x00];

    [Fact]
    public async Task GenerateCommand_WhenRequestBegins_PublishesStartedEvent()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        viewModel.Prompt = "Prompt";
        viewModel.GenerationCount = 2;

        await viewModel.GenerateCommand.ExecuteAsync(null);
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Count(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started) == 2,
            CancellationToken.None);

        IReadOnlyList<GenerationStartSnapshot> starts = lifecycleEventHub.PublishedEvents
            .Where(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started)
            .Select(lifecycleEvent => lifecycleEvent.Start
                ?? throw new InvalidOperationException("Start snapshot is required."))
            .ToList();

        starts.Should().HaveCount(2);
        starts.Should().AllSatisfy(start =>
        {
            start.Prompt.Should().Be("Prompt");
            start.AspectRatio.Should().Be(GenerationAspectRatios.Auto);
            start.GenerationCount.Should().Be(1);
            start.AttachedImagesCount.Should().Be(0);
            start.ModelId.Should().Be(ApiModelMetadataTestCatalog.NanoBanana2ModelId);
        });
    }

    [Fact]
    public async Task GenerateCommand_WhenApiReturnsBatch_PublishesCompletedEvent()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed),
            CancellationToken.None);

        lifecycleEventHub.PublishedEvents
            .Select(lifecycleEvent => lifecycleEvent.Status)
            .Should()
            .Equal(
                GenerationLifecycleStatus.StartRequested,
                GenerationLifecycleStatus.Started,
                GenerationLifecycleStatus.Completed);
        GenerationLifecycleEvent completedEvent = lifecycleEventHub.PublishedEvents
            .Single(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed);
        completedEvent.Batch.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateCommand_WhenApiUnavailable_PublishesFailedEvent()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        ThrowingImageGenerationApiClient apiClient =
            ThrowingImageGenerationApiClient.CreateUnavailableWithRequiredProviderCredential();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(apiClient, lifecycleEventHub);
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);

        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed),
            CancellationToken.None);

        viewModel.ErrorMessage.Should().BeNull();
        viewModel.IsLoading.Should().BeFalse();
        GenerationLifecycleEvent failedEvent = lifecycleEventHub.PublishedEvents
            .Single(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed);
        failedEvent.ErrorMessage.Should().Be(UiStrings.GenerationFailed);
    }

    [Fact]
    public async Task GenerateCommand_WhenRequestCreationFails_SetsSafeErrorMessageWithoutLifecycleEvents()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        viewModel.Prompt = "Prompt";
        viewModel.GenerationCount = 0;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        lifecycleEventHub.PublishedEvents.Should().BeEmpty();
        viewModel.ErrorMessage.Should().Be(UiStrings.GenerationFailed);
    }

    [Fact]
    public async Task GenerateCommand_WithDelayedApi_CompletesBeforeHttpRequest()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        DelayedImageGenerationApiClient apiClient = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(apiClient, lifecycleEventHub);
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);
        await apiClient.RequestReceivedTask;

        viewModel.IsCatalogLoading.Should().BeFalse();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasLoadedCatalog.Should().BeTrue();
        viewModel.GenerateCommand.CanExecute(null).Should().BeTrue();

        apiClient.Complete();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed),
            CancellationToken.None);
    }

    [Fact]
    public async Task GenerateCommand_WithCommandToken_PassesTokenToDispatcher()
    {
        CapturingGenerationRunDispatcher dispatcher = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(dispatcher: dispatcher);
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);

        dispatcher.CapturedCancellationToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateCommand_WithAutoAspectRatio_PassesAutoToRequestAndStartSnapshot()
    {
        CapturingGenerationRunDispatcher dispatcher = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(dispatcher: dispatcher);
        viewModel.Prompt = "Prompt";
        viewModel.SelectedAspectRatio = GenerationAspectRatios.Auto;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        GenerationRunRequest request = dispatcher.CapturedRequest
            ?? throw new InvalidOperationException("Generation run request should be captured.");
        request.Request.AspectRatio.Should().Be(GenerationAspectRatios.Auto);
        request.StartSnapshot.AspectRatio.Should().Be(GenerationAspectRatios.Auto);
    }

    [Fact]
    public async Task GenerateCommand_WithTemperature_PassesTemperatureToRequest()
    {
        CapturingGenerationRunDispatcher dispatcher = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(dispatcher: dispatcher);
        viewModel.Prompt = "Prompt";
        viewModel.Temperature = 1.7d;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        GenerationRunRequest request = dispatcher.CapturedRequest
            ?? throw new InvalidOperationException("Generation run request should be captured.");
        request.Request.Temperature.Should().Be(1.7d);
    }

    [Fact]
    public async Task GenerateCommand_WithHighThinkingLevel_PassesThinkingLevelToRequest()
    {
        CapturingGenerationRunDispatcher dispatcher = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(dispatcher: dispatcher);
        viewModel.Prompt = "Prompt";
        viewModel.SelectedThinkingLevel = viewModel.ThinkingLevels.Single(level => level.Value == "high");

        await viewModel.GenerateCommand.ExecuteAsync(null);

        GenerationRunRequest request = dispatcher.CapturedRequest
            ?? throw new InvalidOperationException("Generation run request should be captured.");
        request.Request.ThinkingLevel.Should().Be("high");
    }

    [Fact]
    public async Task GenerateCommand_WithNanoBananaPro_OmitsThinkingLevelFromRequest()
    {
        CapturingGenerationRunDispatcher dispatcher = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(dispatcher: dispatcher);
        viewModel.Prompt = "Prompt";
        viewModel.SelectedThinkingLevel = viewModel.ThinkingLevels.Single(level => level.Value == "high");
        viewModel.SelectedModel = viewModel.AvailableModels.Single(model =>
            model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);

        await viewModel.GenerateCommand.ExecuteAsync(null);

        GenerationRunRequest request = dispatcher.CapturedRequest
            ?? throw new InvalidOperationException("Generation run request should be captured.");
        request.Request.ThinkingLevel.Should().BeNull();
    }

    [Fact]
    public void SelectedModel_WhenModelsAreSwitched_KeepsThinkingLevelSelection()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        ImageModelOption firstSupportedModel = GetSelectedModel(viewModel);
        ImageModelOption secondSupportedModel = viewModel.AvailableModels.Single(model =>
            model.Thinking is not null
            && !string.Equals(model.Id, firstSupportedModel.Id, StringComparison.Ordinal));
        ImageModelOption unsupportedModel = viewModel.AvailableModels.Single(model =>
            model.Thinking is null);
        viewModel.SelectedThinkingLevel = viewModel.ThinkingLevels.Single(level =>
            string.Equals(level.Value, "high", StringComparison.Ordinal));

        viewModel.SelectedModel = secondSupportedModel;
        viewModel.SelectedModel = unsupportedModel;
        GenerationPanelState unsupportedModelState = stateService.SavedStates.Last();
        viewModel.SelectedModel = firstSupportedModel;

        unsupportedModelState.ThinkingLevel.Should().Be("high");
        viewModel.SelectedThinkingLevel?.Value.Should().Be("high");
    }

    [Fact]
    public async Task GenerateCommand_WithGenerationCountFour_EnqueuesFourSingleGenerationRuns()
    {
        CapturingGenerationRunDispatcher dispatcher = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(dispatcher: dispatcher);
        viewModel.Prompt = "Prompt";
        viewModel.GenerationCount = 4;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        dispatcher.CapturedRequests.Should().HaveCount(4);
        dispatcher.CapturedRequests.Should().AllSatisfy(request =>
        {
            request.Request.GenerationCount.Should().Be(1);
            request.StartSnapshot.GenerationCount.Should().Be(1);
            request.Request.Prompt.Should().Be("Prompt");
        });
    }

    [Fact]
    public async Task GenerateCommand_WithActiveGeneration_AllowsSecondEnqueue()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        DelayedImageGenerationApiClient apiClient = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(apiClient, lifecycleEventHub);
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);
        await apiClient.RequestReceivedTask;

        await viewModel.GenerateCommand.ExecuteAsync(null);
        await AsyncTestWaiter.WaitForConditionAsync(
            () => apiClient.RequestCount == 2,
            CancellationToken.None);

        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasLoadedCatalog.Should().BeTrue();
        viewModel.GenerateCommand.CanExecute(null).Should().BeTrue();

        apiClient.Complete();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Count(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Completed
                    || lifecycleEvent.Status == GenerationLifecycleStatus.StartFailed) == 2,
            CancellationToken.None);
        lifecycleEventHub.PublishedEvents
            .Should()
            .NotContain(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Failed);
    }

    [Fact]
    public async Task GenerateCommand_WhenGoogleApiKeyMissing_SetsSafeErrorMessage()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        RecordingSecretStore secretStore = new(null);
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            lifecycleEventHub: lifecycleEventHub,
            secretStore: secretStore);
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);

        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().Be(UiStrings.GoogleApiKeyMissing);
        lifecycleEventHub.PublishedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateCommand_WithTestModelAndMissingGoogleApiKey_EnqueuesWithoutSecret()
    {
        CapturingGenerationRunDispatcher dispatcher = new();
        RecordingSecretStore secretStore = new(null);
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            secretStore: secretStore,
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateTestModelCatalog()),
            dispatcher: dispatcher);
        viewModel.SelectedModel = viewModel.AvailableModels
            .Single(model => model.Id == TestGenerationModelCatalogAugmenter.ModelId);
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);

        viewModel.ErrorMessage.Should().BeNull();
        secretStore.GetCallCount.Should().Be(0);
        GenerationRunRequest request = dispatcher.CapturedRequest
            ?? throw new InvalidOperationException("Generation run request should be captured.");
        request.Request.ModelId.Should().Be(TestGenerationModelCatalogAugmenter.ModelId);
        request.ProviderCredential.Should().BeEmpty();
    }

    [Fact]
    public async Task AttachImages_WithMultipleValidImages_AddsAllImages()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
        int count = 3;

        await AttachValidImagesAsync(viewModel, count);

        viewModel.AttachedImages.Should().HaveCount(count);
        viewModel.HasErrorMessage.Should().BeFalse();
    }

    [Fact]
    public async Task AttachImages_WithExcessiveImageCount_AttachesAvailableSlotsAndSetsSafeErrorMessage()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
        ImageModelOption selectedModel = GetSelectedModel(viewModel);
        int count = selectedModel.MaxAttachedImages + 1;

        await AttachValidImagesAsync(viewModel, count);

        viewModel.AttachedImages.Should().HaveCount(selectedModel.MaxAttachedImages);
        viewModel.ErrorMessage.Should().Be(UiStrings.ImageAttachmentFailed);
    }

    [Fact]
    public async Task AttachImages_WithExcessiveTotalPreviewBytes_AttachesImagesThatFitAndSetsSafeErrorMessage()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateSmallLimitCatalog()));
        IReadOnlyList<AttachedImageDto> images = Enumerable
            .Range(0, 5)
            .Select(index => CreateLargeAttachedImage(
                $"large-image-{index}.png",
                viewModel.MaxAttachedImageBytes))
            .ToList();

        await viewModel.AttachImagesCommand.ExecuteAsync(images);

        viewModel.AttachedImages.Should().HaveCount(4);
        viewModel.ErrorMessage.Should().Be(UiStrings.ImageAttachmentFailed);
    }

    [Fact]
    public async Task AttachImages_WhenLimitReached_DisablesAttachmentCommands()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
        await FillAttachedImagesToLimitAsync(viewModel);
        List<AttachedImageDto> extraImages = [CreateAttachedImage("extra.png")];

        bool canAttachMore = viewModel.AttachImagesCommand.CanExecute(extraImages);
        bool canPickMore = viewModel.PickImageCommand.CanExecute(null);

        viewModel.AttachedImages.Should().HaveCount(GetSelectedModel(viewModel).MaxAttachedImages);
        canAttachMore.Should().BeFalse();
        canPickMore.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateCommand_WhileAttachmentIsPreparing_IsDisabledUntilCancellation()
    {
        BlockingAttachedImagePreparationService preparationService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            attachmentPreparationService: preparationService);
        viewModel.Prompt = "Prompt";
        List<AttachedImageDto> images = [CreateAttachedImage("pending.png")];

        Task attaching = viewModel.AttachImagesCommand.ExecuteAsync(images);
        await preparationService.WaitUntilStartedAsync();
        AttachedImageViewModel pendingImage = viewModel.AttachedImages.Single();

        viewModel.IsAttaching.Should().BeTrue();
        viewModel.GenerateCommand.CanExecute(null).Should().BeFalse();
        await viewModel.RemoveAttachmentCommand.ExecuteAsync(pendingImage);

        viewModel.IsAttaching.Should().BeFalse();
        viewModel.GenerateCommand.CanExecute(null).Should().BeTrue();
        await attaching;
    }

    [Fact]
    public async Task AttachImagesCommand_WhenSecondExecutionStarts_KeepsFirstAttachmentRunning()
    {
        AttachedImageDto firstImage = CreateAttachedImage("first.png");
        AttachedImageDto secondImage = CreateAttachedImage("second.png");
        ControlledAttachedImagePreparationService preparationService = new(
            [firstImage.FileName, secondImage.FileName]);
        GenerationModelCatalogDto singleAttachmentCatalog = CreateCompatibilityCatalog(
            [GenerationAspectRatios.Auto],
            ["1K"],
            [1],
            [GenerationAspectRatios.Auto],
            ["1K"],
            [1]);
        GenerationModelCatalogDto catalog = new(
            singleAttachmentCatalog.Models
                .Select(model => model with
                {
                    Attachments = new GenerationModelAttachmentMetadataDto(
                        8,
                        1024,
                        8192,
                        model.Attachments.SupportedContentTypes)
                })
                .ToList());
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: new SuccessfulGenerationModelCatalogApiClient(catalog),
            imageModelOptionCatalog: CreateImageModelOptionCatalog(catalog),
            attachmentPreparationService: preparationService);

        Task firstAttachment = viewModel.AttachImagesCommand.ExecuteAsync(
            [firstImage]);
        await preparationService.WaitUntilStartedAsync(firstImage.FileName);
        Task secondAttachment = viewModel.AttachImagesCommand.ExecuteAsync(
            [secondImage]);
        await preparationService.WaitUntilStartedAsync(secondImage.FileName);
        preparationService.Complete(secondImage);
        await secondAttachment;

        viewModel.AttachedImages.Should().HaveCount(2);
        viewModel.AttachedImages[0].IsLoading.Should().BeTrue();
        viewModel.AttachedImages[1].IsReady.Should().BeTrue();

        preparationService.Complete(firstImage);
        await firstAttachment;

        viewModel.AttachedImages.Should().OnlyContain(image => image.IsReady);
    }

    [Fact]
    public void AttachmentCounterText_WithDefaultModel_ShowsCurrentAndMaxCount()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
        ImageModelOption selectedModel = GetSelectedModel(viewModel);

        viewModel.AttachmentCounterText.Should().Be($"0/{selectedModel.MaxAttachedImages}");
    }

    [Fact]
    public async Task AttachmentCounterText_WhenImagesAttached_ShowsCurrentAndMaxCount()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
        int count = 2;

        await AttachValidImagesAsync(viewModel, count);

        viewModel.AttachmentCounterText.Should().Be($"{count}/{GetSelectedModel(viewModel).MaxAttachedImages}");
    }

    [Fact]
    public async Task AttachImages_WithInvalidImageSignature_SetsSafeErrorMessage()
    {
        AttachedImageDto invalidImage = new(
            "image.png",
            GenerationImageContentTypes.Png,
            [0x00, 0x01, 0x02]);
        List<AttachedImageDto> images = [invalidImage];

        await AssertImageAttachmentRejectedAsync(images);
    }

    [Fact]
    public async Task AttachImages_WithNullImage_SetsSafeErrorMessage()
    {
        IReadOnlyList<AttachedImageDto> images = CreateAttachedImagesWithNull();

        await AssertImageAttachmentRejectedAsync(images);
    }

    [Fact]
    public async Task AttachImages_WithUnsupportedContentType_SetsSafeErrorMessage()
    {
        AttachedImageDto invalidImage = new("image.gif", "image/gif", PngBytes);
        List<AttachedImageDto> images = [invalidImage];

        await AssertImageAttachmentRejectedAsync(images);
    }

    [Fact]
    public async Task LoadModelCatalogCommand_WhenApiReturnsCatalog_PopulatesOptions()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(initializeCatalog: false);

        await viewModel.LoadModelCatalogCommand.ExecuteAsync(null);

        viewModel.HasLoadedCatalog.Should().BeTrue();
        viewModel.GenerateCommand.CanExecute(null).Should().BeFalse();
        viewModel.AvailableModels.Should().HaveCount(ApiModelMetadataTestCatalog.LoadCatalog().Models.Count);
        viewModel.AvailableModels.Select(model => model.Id).Should().Contain(ApiModelMetadataTestCatalog.NanoBanana2ModelId);
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        ImageModelOption selectedModel = GetSelectedModel(viewModel);
        selectedModel.Id.Should().Be(ApiModelMetadataTestCatalog.NanoBanana2ModelId);
        selectedModel.AspectRatios.Should().Equal(metadata.AspectRatios);
        selectedModel.Resolutions.Should().Equal(metadata.Resolutions);
        selectedModel.GenerationCounts.Should().Equal(metadata.GenerationCounts);
        selectedModel.Temperature.Should().Be(metadata.Temperature);
        selectedModel.Thinking.Should().BeEquivalentTo(metadata.Thinking);
        selectedModel.MaxAttachedImages.Should().Be(metadata.Attachments.MaxCount);
        selectedModel.MaxAttachedImageBytes.Should().Be((int)metadata.Attachments.MaxSingleFileBytes);
        selectedModel.MaxTotalAttachedImageBytes.Should().Be(metadata.Attachments.MaxTotalBytes);
        selectedModel.SupportedAttachmentContentTypes.Should().Equal(metadata.Attachments.SupportedContentTypes);
        selectedModel.PanelId.Should().Be(metadata.PanelId);
        viewModel.SelectedAspectRatio.Should().Be(metadata.AspectRatios.First());
        viewModel.SelectedAspectRatio.Should().Be(GenerationAspectRatios.Auto);
        viewModel.SelectedResolution.Should().Be(metadata.Resolutions.First());
        viewModel.MinimumTemperature.Should().Be(metadata.Temperature.Minimum);
        viewModel.MaximumTemperature.Should().Be(metadata.Temperature.Maximum);
        viewModel.DefaultTemperature.Should().Be(metadata.Temperature.Default);
        viewModel.TemperatureStep.Should().Be(metadata.Temperature.Step);
        viewModel.Temperature.Should().Be(metadata.Temperature.Default);
        viewModel.SupportsThinkingLevel.Should().BeTrue();
        viewModel.ThinkingLevels.Select(level => level.DisplayName).Should().Equal("Минимальный", "Максимальный");
        viewModel.SelectedThinkingLevel?.Value.Should().Be(metadata.Thinking?.Default);
        viewModel.GenerationCount.Should().Be(metadata.GenerationCounts.First());
        viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadModelCatalogCommand_WhenCatalogContainsNanoBananaPro_UsesProMetadata()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(initializeCatalog: false);

        await viewModel.LoadModelCatalogCommand.ExecuteAsync(null);

        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
        ImageModelOption proModel = viewModel.AvailableModels
            .Single(model => model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);
        viewModel.SelectedModel = proModel;

        viewModel.SupportsModel(ApiModelMetadataTestCatalog.NanoBanana2ModelId).Should().BeTrue();
        viewModel.SupportsModel(ApiModelMetadataTestCatalog.NanoBananaProModelId).Should().BeTrue();
        metadata.PanelId.Should().Be(ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().PanelId);
        viewModel.SelectedModel.Id.Should().Be(ApiModelMetadataTestCatalog.NanoBananaProModelId);
        viewModel.AspectRatios.Should().Equal(metadata.AspectRatios);
        viewModel.Resolutions.Should().Equal(metadata.Resolutions);
        viewModel.GenerationCounts.Should().Equal(metadata.GenerationCounts);
        viewModel.SelectedAspectRatio.Should().Be(metadata.AspectRatios.First());
        viewModel.SelectedResolution.Should().Be(metadata.Resolutions.First());
        viewModel.MaxAttachedImageBytes.Should().Be((int)metadata.Attachments.MaxSingleFileBytes);
        viewModel.SupportsThinkingLevel.Should().BeFalse();
        viewModel.ThinkingLevels.Should().BeEmpty();
        viewModel.SelectedThinkingLevel.Should().BeNull();
    }

    [Fact]
    public async Task LoadModelCatalogCommand_WithSavedPanelState_RestoresPanelValues()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
        RecordingGenerationPanelStateService stateService = CreateStateService(
            metadata,
            metadata.AspectRatios.Last(),
            metadata.Resolutions.Last(),
            metadata.GenerationCounts.Last(),
            "restored prompt",
            temperature: 1.7d);
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService,
            initializeCatalog: false);

        await viewModel.LoadModelCatalogCommand.ExecuteAsync(null);

        viewModel.PanelId.Should().Be(GenerationPanelIds.NanoBanana);
        viewModel.SelectedModel?.Id.Should().Be(metadata.Id);
        viewModel.SelectedAspectRatio.Should().Be(metadata.AspectRatios.Last());
        viewModel.SelectedResolution.Should().Be(metadata.Resolutions.Last());
        viewModel.Temperature.Should().Be(1.7d);
        viewModel.GenerationCount.Should().Be(metadata.GenerationCounts.Last());
        viewModel.Prompt.Should().Be("restored prompt");
    }

    [Fact]
    public async Task RestorePanelStateCommand_WithLoadedCatalog_RestoresPanelValuesWithoutCatalogCommand()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
        RecordingGenerationPanelStateService stateService = CreateStateService(
            metadata,
            metadata.AspectRatios.Last(),
            metadata.Resolutions.Last(),
            metadata.GenerationCounts.Last(),
            "constructor restored prompt");
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);

        await viewModel.RestorePanelStateCommand.ExecuteAsync(null);

        stateService.LoadCallCount.Should().Be(1);
        viewModel.LoadModelCatalogCommand.CanExecute(null).Should().BeFalse();
        viewModel.SelectedModel?.Id.Should().Be(metadata.Id);
        viewModel.SelectedAspectRatio.Should().Be(metadata.AspectRatios.Last());
        viewModel.SelectedResolution.Should().Be(metadata.Resolutions.Last());
        viewModel.GenerationCount.Should().Be(metadata.GenerationCounts.Last());
    }

    [Fact]
    public async Task RestorePanelStateCommand_WithNormalizedState_AppliesServiceSnapshot()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
        RecordingGenerationPanelStateService stateService = CreateStateService(
            metadata,
            metadata.AspectRatios.Last(),
            metadata.Resolutions.Last(),
            metadata.GenerationCounts.Last(),
            "restored prompt");
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);

        await viewModel.RestorePanelStateCommand.ExecuteAsync(null);

        viewModel.SelectedModel?.Id.Should().Be(metadata.Id);
        viewModel.SelectedAspectRatio.Should().Be(metadata.AspectRatios.Last());
        viewModel.SelectedResolution.Should().Be(metadata.Resolutions.Last());
        viewModel.GenerationCount.Should().Be(metadata.GenerationCounts.Last());
        viewModel.Prompt.Should().Be("restored prompt");
    }

    [Theory]
    [InlineData("999:1", "999K", 999)]
    [InlineData("", "", 0)]
    public async Task RestorePanelStateCommand_WithUnavailableOptions_RestoresModelDefaultsWithoutSelectionResetNotifications(
        string aspectRatio,
        string resolution,
        int generationCount)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
        RecordingGenerationPanelStateService stateService = CreateStateService(
            metadata,
            aspectRatio,
            resolution,
            generationCount,
            "restored prompt");
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        SelectionValueResetRecorder resetRecorder = new(viewModel);

        await viewModel.RestorePanelStateCommand.ExecuteAsync(null);

        viewModel.SelectedModel?.Id.Should().Be(metadata.Id);
        viewModel.SelectedAspectRatio.Should().Be(metadata.AspectRatios.First());
        viewModel.SelectedResolution.Should().Be(metadata.Resolutions.First());
        viewModel.GenerationCount.Should().Be(metadata.GenerationCounts.First());
        viewModel.Prompt.Should().Be("restored prompt");
        resetRecorder.AssertCounts(0, 0, 0);
    }

    [Fact]
    public async Task RestorePanelStateCommand_WhenStateServiceThrows_SetsUserSafeErrorMessage()
    {
        const string exceptionMessage = "raw state store failure";

        TestViewModelErrorHandler errorHandler = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            errorHandler: errorHandler,
            generationPanelStateService: new ThrowingGenerationPanelStateService(exceptionMessage));

        await viewModel.RestorePanelStateCommand.ExecuteAsync(null);

        errorHandler.LogCallCount.Should().Be(1);
        viewModel.ErrorMessage.Should().Be(UiStrings.GenerationFailed);
        viewModel.ErrorMessage.Should().NotContain(exceptionMessage);
    }

    [Fact]
    public void AspectRatioHintPreviewSizer_WithWideRatio_FitsMaxBounds()
    {
        AspectRatioHintPreviewSize size = AspectRatioHintPreviewSizer.Calculate(
            "16:9",
            360d,
            220d);

        size.Width.Should().Be(360d);
        size.Height.Should().BeApproximately(202.5d, 0.001d);
    }

    [Fact]
    public void AspectRatioHintPreviewSizer_WithTallRatio_FitsMaxBounds()
    {
        AspectRatioHintPreviewSize size = AspectRatioHintPreviewSizer.Calculate(
            "9:16",
            360d,
            220d);

        size.Width.Should().BeApproximately(123.75d, 0.001d);
        size.Height.Should().Be(220d);
    }

    [Fact]
    public async Task GenerateCommand_WhenNanoBananaProSelected_PublishesProModelId()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(lifecycleEventHub: lifecycleEventHub);
        ImageModelOption proModel = viewModel.AvailableModels
            .Single(model => model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);
        viewModel.SelectedModel = proModel;
        viewModel.Prompt = "Prompt";

        await viewModel.GenerateCommand.ExecuteAsync(null);
        await AsyncTestWaiter.WaitForConditionAsync(
            () => lifecycleEventHub.PublishedEvents.Any(
                lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started),
            CancellationToken.None);

        GenerationLifecycleEvent startedEvent = lifecycleEventHub.PublishedEvents
            .Single(lifecycleEvent => lifecycleEvent.Status == GenerationLifecycleStatus.Started);
        GenerationStartSnapshot start = startedEvent.Start
            ?? throw new InvalidOperationException("Start snapshot is required.");
        start.ModelId.Should().Be(ApiModelMetadataTestCatalog.NanoBananaProModelId);
        start.ModelDisplayName.Should().Be(proModel.DisplayName);
        start.Resolution.Should().Be(ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata().Resolutions.First());
    }

    [Fact]
    public async Task LoadModelCatalogCommand_WhileApiPending_SetsCatalogLoadingOnly()
    {
        DelayedGenerationModelCatalogApiClient catalogApiClient = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: catalogApiClient,
            initializeCatalog: false);

        Task loadTask = viewModel.LoadModelCatalogCommand.ExecuteAsync(null);
        await catalogApiClient.RequestReceivedTask;

        viewModel.IsCatalogLoading.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.HasLoadedCatalog.Should().BeFalse();

        catalogApiClient.Complete();
        await loadTask;

        viewModel.IsCatalogLoading.Should().BeFalse();
        viewModel.HasLoadedCatalog.Should().BeTrue();
    }

    [Fact]
    public async Task LoadModelCatalogCommand_WhenApiThrows_SetsSafeErrorMessage()
    {
        TestViewModelErrorHandler errorHandler = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: new ThrowingGenerationModelCatalogApiClient(),
            initializeCatalog: false,
            errorHandler: errorHandler);

        await viewModel.LoadModelCatalogCommand.ExecuteAsync(null);

        viewModel.HasLoadedCatalog.Should().BeFalse();
        viewModel.GenerateCommand.CanExecute(null).Should().BeFalse();
        viewModel.ErrorMessage.Should().Be(UiStrings.ModelCatalogLoadFailed);
        errorHandler.LogCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PrepareStateRestoreAsync_AfterSavedEndpointApplied_LoadsCatalogOnce()
    {
        SuccessfulGenerationModelCatalogApiClient catalogApiClient = new();
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        using UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: catalogApiClient,
            apiEndpointService: endpointService,
            initializeCatalog: false);
        SetApiBaseAddress(endpointService, "https://saved.atomicart.test/");

        await viewModel.PrepareStateRestoreAsync(CancellationToken.None);

        catalogApiClient.CallCount.Should().Be(1);
        viewModel.HasLoadedCatalog.Should().BeTrue();
        viewModel.AvailableModels.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ApiBaseAddressChanged_WithLoadedCatalog_ClearsAndReloadsCatalog()
    {
        DelayedGenerationModelCatalogApiClient catalogApiClient = new();
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        using UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: catalogApiClient,
            apiEndpointService: endpointService);
        await viewModel.PrepareStateRestoreAsync(CancellationToken.None);

        SetApiBaseAddress(endpointService, "https://second.atomicart.test/");
        await catalogApiClient.RequestReceivedTask;

        viewModel.IsCatalogLoading.Should().BeTrue();
        viewModel.HasLoadedCatalog.Should().BeFalse();
        viewModel.AvailableModels.Should().BeEmpty();
        catalogApiClient.Complete();
        await AsyncTestWaiter.WaitForConditionAsync(
            () => viewModel.HasLoadedCatalog,
            CancellationToken.None);

        viewModel.AvailableModels.Should().NotBeEmpty();
        viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ApiBaseAddressChanged_WhenReloadFails_KeepsCatalogUnavailable()
    {
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        using UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: new ThrowingGenerationModelCatalogApiClient(),
            apiEndpointService: endpointService);
        await viewModel.PrepareStateRestoreAsync(CancellationToken.None);

        SetApiBaseAddress(endpointService, "https://unavailable.atomicart.test/");
        await AsyncTestWaiter.WaitForConditionAsync(
            () => !viewModel.IsCatalogLoading
                && viewModel.ErrorMessage == UiStrings.ModelCatalogLoadFailed,
            CancellationToken.None);

        endpointService.BaseAddress.ToString().Should().Be(
            "https://unavailable.atomicart.test/");
        viewModel.HasLoadedCatalog.Should().BeFalse();
        viewModel.AvailableModels.Should().BeEmpty();
    }

    [Fact]
    public async Task ApiBaseAddressChanged_WhenOlderResponseArrivesLast_IgnoresOlderResponse()
    {
        SequencedGenerationModelCatalogApiClient catalogApiClient = new();
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create();
        using UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: catalogApiClient,
            apiEndpointService: endpointService);
        await viewModel.PrepareStateRestoreAsync(CancellationToken.None);

        SetApiBaseAddress(endpointService, "https://second.atomicart.test/");
        await AsyncTestWaiter.WaitForConditionAsync(
            () => catalogApiClient.RequestCount == 1,
            CancellationToken.None);
        SetApiBaseAddress(endpointService, "https://third.atomicart.test/");
        await AsyncTestWaiter.WaitForConditionAsync(
            () => catalogApiClient.RequestCount == 2,
            CancellationToken.None);
        catalogApiClient.Complete(1, ApiModelMetadataTestCatalog.LoadCatalog());
        await AsyncTestWaiter.WaitForConditionAsync(
            () => viewModel.HasLoadedCatalog,
            CancellationToken.None);
        string selectedModelId = viewModel.SelectedModel?.Id
            ?? throw new InvalidOperationException("Selected model is required.");

        catalogApiClient.Complete(
            0,
            CreateCompatibilityCatalog(
                ["1:1"],
                ["1k"],
                [1],
                ["16:9"],
                ["2k"],
                [2]));
        await AsyncTestWaiter.WaitForConditionAsync(
            () => catalogApiClient.ReturnedResponseCount == 2,
            CancellationToken.None);

        selectedModelId.Should().Be(ApiModelMetadataTestCatalog.NanoBanana2ModelId);
        viewModel.SelectedModel?.Id.Should().Be(selectedModelId);
        viewModel.AvailableModels.Should().NotContain(model =>
            model.Id.StartsWith("compat-model", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadModelCatalogCommand_WhenCatalogContainsGoogleOtherPanel_DoesNotSelectUnsupportedModel()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            catalogApiClient: new SuccessfulGenerationModelCatalogApiClient(CreateCatalogWithOtherModel()),
            initializeCatalog: false);

        await viewModel.LoadModelCatalogCommand.ExecuteAsync(null);

        viewModel.AvailableModels.Should().BeEmpty();
        viewModel.SelectedModel.Should().BeNull();
        viewModel.SupportsModel("other-model").Should().BeFalse();
        viewModel.HasLoadedCatalog.Should().BeFalse();
        viewModel.GenerateCommand.CanExecute(null).Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void SelectedModel_WhenChanged_SavesPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        ImageModelOption proModel = viewModel.AvailableModels
            .Single(model => model.Id == ApiModelMetadataTestCatalog.NanoBananaProModelId);

        viewModel.SelectedModel = proModel;

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.SelectedModelId.Should().Be(proModel.Id);
        savedState.PanelId.Should().Be(viewModel.PanelId);
    }

    [Fact]
    public void SelectedModel_WhenAspectRatioSupportedByNewModel_KeepsAspectRatioWithoutSelectionResetNotification()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["1:1", "16:9"],
                ["1K"],
                [1],
                [GenerationAspectRatios.Auto, "16:9"],
                ["1K"],
                [1])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        viewModel.SelectedAspectRatio = "16:9";

        viewModel.SelectedModel = secondModel;

        viewModel.SelectedAspectRatio.Should().Be("16:9");
        resetRecorder.CountFor(nameof(UniversalNanoBananaPanelViewModel.SelectedAspectRatio)).Should().Be(0);
    }

    [Fact]
    public void SelectedModel_WhenAspectRatioUnsupportedByNewModel_ResetsToDefaultAndRaisesSelectionResetNotification()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["1:1", "16:9"],
                ["1K"],
                [1],
                [GenerationAspectRatios.Auto, "1:1"],
                ["1K"],
                [1])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        viewModel.SelectedAspectRatio = "16:9";

        viewModel.SelectedModel = secondModel;

        viewModel.SelectedAspectRatio.Should().Be(GenerationAspectRatios.Auto);
        resetRecorder.CountFor(nameof(UniversalNanoBananaPanelViewModel.SelectedAspectRatio)).Should().Be(1);
    }

    [Fact]
    public void SelectedModel_WhenResolutionSupportedByNewModel_KeepsResolutionWithoutSelectionResetNotification()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["1:1"],
                ["1K", "2K"],
                [1],
                ["1:1"],
                ["512", "2K"],
                [1])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        viewModel.SelectedResolution = "2K";

        viewModel.SelectedModel = secondModel;

        viewModel.SelectedResolution.Should().Be("2K");
        resetRecorder.CountFor(nameof(UniversalNanoBananaPanelViewModel.SelectedResolution)).Should().Be(0);
    }

    [Fact]
    public void SelectedModel_WhenResolutionUnsupportedByNewModel_ResetsToDefaultAndRaisesSelectionResetNotification()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["1:1"],
                ["1K", "2K"],
                [1],
                ["1:1"],
                ["512", "1K"],
                [1])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        viewModel.SelectedResolution = "2K";

        viewModel.SelectedModel = secondModel;

        viewModel.SelectedResolution.Should().Be("512");
        resetRecorder.CountFor(nameof(UniversalNanoBananaPanelViewModel.SelectedResolution)).Should().Be(1);
    }

    [Fact]
    public void SelectedModel_WhenGenerationCountSupportedByNewModel_KeepsGenerationCountWithoutSelectionResetNotification()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["1:1"],
                ["1K"],
                [1, 4],
                ["1:1"],
                ["1K"],
                [2, 4])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        viewModel.GenerationCount = 4;

        viewModel.SelectedModel = secondModel;

        viewModel.GenerationCount.Should().Be(4);
        resetRecorder.CountFor(nameof(UniversalNanoBananaPanelViewModel.GenerationCount)).Should().Be(0);
    }

    [Fact]
    public void SelectedModel_WhenGenerationCountUnsupportedByNewModel_ResetsToDefaultAndRaisesSelectionResetNotification()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["1:1"],
                ["1K"],
                [1, 4],
                ["1:1"],
                ["1K"],
                [2, 3])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        viewModel.GenerationCount = 4;

        viewModel.SelectedModel = secondModel;

        viewModel.GenerationCount.Should().Be(2);
        resetRecorder.CountFor(nameof(UniversalNanoBananaPanelViewModel.GenerationCount)).Should().Be(1);
    }

    [Fact]
    public void SelectedModel_WhenSwitchedBack_DoesNotRestorePreviousModelValues()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["16:9", "1:1"],
                ["2K", "1K"],
                [4, 1],
                ["1:1"],
                ["1K"],
                [1])));
        ImageModelOption firstModel = GetModel(viewModel, "compat-model-a");
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        viewModel.SelectedAspectRatio = "16:9";
        viewModel.SelectedResolution = "2K";
        viewModel.GenerationCount = 4;

        viewModel.SelectedModel = secondModel;
        viewModel.SelectedModel = firstModel;

        viewModel.SelectedAspectRatio.Should().Be("1:1");
        viewModel.SelectedResolution.Should().Be("1K");
        viewModel.GenerationCount.Should().Be(1);
    }

    [Fact]
    public void SelectedModel_WhenChangedAfterCompatibilityReset_SavesActualPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["16:9"],
                ["2K"],
                [4],
                ["1:1"],
                ["1K"],
                [1])),
            generationPanelStateService: stateService);
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        stateService.SavedStates.Clear();

        viewModel.SelectedModel = secondModel;

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.SelectedModelId.Should().Be(secondModel.Id);
        savedState.AspectRatio.Should().Be("1:1");
        savedState.Resolution.Should().Be("1K");
        savedState.GenerationCount.Should().Be(1);
    }

    [Fact]
    public void SelectedModel_WhenBindingsClearUnsupportedValuesAfterItemsSourceRefresh_ResetsFromPreviousValuesAndRaisesSelectionResetNotifications()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["16:9"],
                ["2K"],
                [4],
                ["1:1"],
                ["1K"],
                [1])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        SimulateBindingsClearingSelectionsAfterOptionSourcesRefresh(viewModel);

        viewModel.SelectedModel = secondModel;

        viewModel.SelectedAspectRatio.Should().Be("1:1");
        viewModel.SelectedResolution.Should().Be("1K");
        viewModel.GenerationCount.Should().Be(1);
        resetRecorder.AssertCounts(1, 1, 1);
    }

    [Fact]
    public void SelectedModel_WhenBindingsClearSupportedValuesAfterItemsSourceRefresh_RestoresPreviousValuesWithoutSelectionResetNotifications()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageModelOptionCatalog: CreateImageModelOptionCatalog(CreateCompatibilityCatalog(
                ["16:9"],
                ["2K"],
                [4],
                ["16:9", "1:1"],
                ["2K", "1K"],
                [4, 1])));
        ImageModelOption secondModel = GetModel(viewModel, "compat-model-b");
        SelectionValueResetRecorder resetRecorder = new(viewModel);
        SimulateBindingsClearingSelectionsAfterOptionSourcesRefresh(viewModel);

        viewModel.SelectedModel = secondModel;

        viewModel.SelectedAspectRatio.Should().Be("16:9");
        viewModel.SelectedResolution.Should().Be("2K");
        viewModel.GenerationCount.Should().Be(4);
        resetRecorder.AssertCounts(0, 0, 0);
    }

    [Fact]
    public void SelectedResolution_WhenChanged_SavesPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        string resolution = GetSelectedModel(viewModel).Resolutions.Last();

        viewModel.SelectedResolution = resolution;

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.Resolution.Should().Be(resolution);
    }

    [Fact]
    public void Temperature_WhenChanged_SavesPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);

        viewModel.Temperature = 1.7d;

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.Temperature.Should().Be(1.7d);
    }

    [Fact]
    public void SelectedThinkingLevel_WhenChanged_SavesPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        GenerationModelThinkingLevelMetadataDto highLevel = viewModel.ThinkingLevels
            .Single(level => level.Value == "high");

        viewModel.SelectedThinkingLevel = highLevel;

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.ThinkingLevel.Should().Be("high");
    }

    [Fact]
    public void ResetThinkingLevelCommand_WithChangedLevel_RestoresApiDefaultAndSavesPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        GenerationModelThinkingMetadataDto thinking = GetSelectedModel(viewModel).Thinking
            ?? throw new InvalidOperationException("Selected model must support thinking levels.");
        viewModel.SelectedThinkingLevel = viewModel.ThinkingLevels.Single(level => level.Value == "high");
        stateService.SavedStates.Clear();

        viewModel.ResetThinkingLevelCommand.Execute(null);

        viewModel.SelectedThinkingLevel?.Value.Should().Be(thinking.Default);
        stateService.SavedStates.Should()
            .ContainSingle()
            .Which.ThinkingLevel.Should().Be(thinking.Default);
    }

    [Fact]
    public void ResetTemperatureCommand_WithChangedTemperature_RestoresDefaultAndSavesPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        double defaultTemperature = GetSelectedModel(viewModel).Temperature.Default;
        viewModel.Temperature = 1.7d;
        stateService.SavedStates.Clear();

        viewModel.ResetTemperatureCommand.Execute(null);

        viewModel.Temperature.Should().Be(defaultTemperature);
        stateService.SavedStates.Should()
            .ContainSingle()
            .Which.Temperature.Should().Be(defaultTemperature);
    }

    [Fact]
    public async Task AttachImagesCommand_WithValidImage_SavesAttachmentAndPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        RecordingPanelAttachmentStore attachmentStore = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService,
            attachmentStore: attachmentStore);

        await AttachImageAsync(viewModel, "attachment.png");

        attachmentStore.SaveCallCount.Should().Be(1);
        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.Attachments.Should().ContainSingle()
            .Which.FileName.Should().Be("attachment.png");
    }

    [Fact]
    public async Task RemoveAttachmentCommand_WithExistingAttachment_SavesPanelState()
    {
        RecordingGenerationPanelStateService stateService = new();
        RecordingPanelAttachmentStore attachmentStore = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService,
            attachmentStore: attachmentStore);
        AttachedImageViewModel attachedImage = await AttachImageAsync(viewModel, "attachment.png");

        await viewModel.RemoveAttachmentCommand.ExecuteAsync(attachedImage);

        attachmentStore.DeleteCallCount.Should().Be(1);
        stateService.SavedStates.Should().HaveCount(2);
        stateService.SavedStates.Last().Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenAttachmentCommand_WithExistingAttachment_OpensViewerWithAttachedImageSource()
    {
        RecordingImageViewerService imageViewerService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageViewerService: imageViewerService);
        AttachedImageViewModel attachedImage = await AttachImageAsync(viewModel, "attachment.png");

        await viewModel.OpenAttachmentCommand.ExecuteAsync(attachedImage);

        GalleryImageViewerRequest request = imageViewerService.LastRequest
            ?? throw new InvalidOperationException("Image viewer request should be captured.");
        request.SelectedItemId.Should().Be(attachedImage.Id);
        GalleryImageViewerItem item = request.ItemsSource.GetItems().Should()
            .ContainSingle()
            .Subject;
        item.Id.Should().Be(attachedImage.Id);
        GalleryAttachedImageViewerSource source = item.Source.Should()
            .BeOfType<GalleryAttachedImageViewerSource>()
            .Which;
        source.Image.FileName.Should().Be("attachment.png");
        source.Image.Content.Should().Equal(PngBytes);
    }

    [Fact]
    public async Task OpenAttachmentCommand_AfterAttachmentAdded_ViewerItemsSourceReturnsCurrentAttachments()
    {
        RecordingImageViewerService imageViewerService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageViewerService: imageViewerService);
        AttachedImageViewModel attachedImage = await AttachImageAsync(viewModel, "first.png");
        await viewModel.OpenAttachmentCommand.ExecuteAsync(attachedImage);
        GalleryImageViewerRequest request = imageViewerService.LastRequest
            ?? throw new InvalidOperationException("Image viewer request should be captured.");

        await AttachImageAsync(viewModel, "second.png");

        IReadOnlyList<string> expectedFileNames = viewModel.AttachedImages
            .Select(image => image.FileName)
            .ToList();
        request.ItemsSource.GetItems()
            .Select(GetAttachedSourceFileName)
            .Should()
            .Equal(expectedFileNames);
    }

    [Fact]
    public async Task OpenAttachmentCommand_WhileAnotherAttachmentIsPreparing_ViewerContainsOnlyReadyAttachments()
    {
        AttachedImageDto readyImageDto = CreateAttachedImage("ready.png");
        AttachedImageDto pendingImageDto = CreateAttachedImage("pending.png");
        ControlledAttachedImagePreparationService preparationService = new(
            [readyImageDto.FileName, pendingImageDto.FileName]);
        RecordingImageViewerService imageViewerService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            imageViewerService: imageViewerService,
            attachmentPreparationService: preparationService);
        Task readyAttaching = viewModel.AttachImagesCommand.ExecuteAsync(
            [readyImageDto]);
        await preparationService.WaitUntilStartedAsync(readyImageDto.FileName);
        preparationService.Complete(readyImageDto);
        await readyAttaching;
        AttachedImageViewModel readyImage = viewModel.AttachedImages.Single();
        Task pendingAttaching = viewModel.AttachImagesCommand.ExecuteAsync(
            [pendingImageDto]);
        await preparationService.WaitUntilStartedAsync(pendingImageDto.FileName);
        AttachedImageViewModel pendingImage = viewModel.AttachedImages
            .Single(image => !image.IsReady);

        await viewModel.OpenAttachmentCommand.ExecuteAsync(readyImage);

        GalleryImageViewerRequest request = imageViewerService.LastRequest
            ?? throw new InvalidOperationException("Image viewer request should be captured.");
        GalleryImageViewerItem viewerItem = request.ItemsSource
            .GetItems()
            .Should()
            .ContainSingle()
            .Subject;
        viewerItem.Id.Should().Be(readyImage.Id);
        GetAttachedSourceFileName(viewerItem).Should().Be(readyImageDto.FileName);
        viewModel.ErrorMessage.Should().BeNull();

        await viewModel.RemoveAttachmentCommand.ExecuteAsync(pendingImage);
        await pendingAttaching;
    }

    [Fact]
    public void PricePreview_WhenGenerationCountChanges_UpdatesGenerateButtonText()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
        string initialText = viewModel.GenerateButtonText;

        viewModel.GenerationCount = 2;

        viewModel.GenerateButtonText.Should().NotBe(initialText);
        viewModel.GenerateButtonText.Should().Contain("USD");
    }

    [Fact]
    public async Task CommitPromptCommand_AfterPromptChanges_SavesPromptOnce()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);

        viewModel.Prompt = "first";
        viewModel.Prompt = "second";

        stateService.SavedStates.Should().BeEmpty();

        await viewModel.CommitPromptCommand.ExecuteAsync(null);

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.Prompt.Should().Be("second");

        await Task.Delay(TimeSpan.FromMilliseconds(PromptDelayedSaveSettleMilliseconds));

        stateService.SavedStates.Should().ContainSingle();
    }

    [Fact]
    public async Task CommitPromptCommand_WithEmptyPrompt_SavesExplicitEmptyPrompt()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);

        viewModel.Prompt = "temporary";
        viewModel.Prompt = string.Empty;

        await viewModel.CommitPromptCommand.ExecuteAsync(null);

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.Prompt.Should().BeEmpty();
    }

    [Fact]
    public async Task PromptChanged_AfterPause_SavesPromptOnce()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);

        viewModel.Prompt = "first";
        viewModel.Prompt = "second";

        stateService.SavedStates.Should().BeEmpty();

        await AsyncTestWaiter.WaitForConditionAsync(
            () => stateService.SavedStates.Count == 1,
            CancellationToken.None);

        GenerationPanelState savedState = stateService.SavedStates
            .Should()
            .ContainSingle()
            .Subject;
        savedState.Prompt.Should().Be("second");
    }

    [Fact]
    public async Task CommitPendingStateAsync_WithPromptDelayPending_SavesCurrentPrompt()
    {
        RecordingGenerationPanelStateService stateService = new();
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel(
            generationPanelStateService: stateService);
        IAppStateGenerationPanelFlushTarget flushTarget = viewModel;

        viewModel.Prompt = "pending prompt";

        stateService.SavedStates.Should().BeEmpty();

        await flushTarget.CommitPendingStateAsync(CancellationToken.None);

        GenerationPanelState savedState = stateService.SavedStates.Should()
            .ContainSingle()
            .Subject;
        savedState.Prompt.Should().Be("pending prompt");

        await Task.Delay(TimeSpan.FromMilliseconds(PromptDelayedSaveSettleMilliseconds));

        stateService.SavedStates.Should().ContainSingle();
    }

    [Fact]
    public async Task PricePreview_WhenAttachmentAdded_UpdatesGenerateButtonText()
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();
        string initialText = viewModel.GenerateButtonText;

        await AttachValidImagesAsync(viewModel, 1);

        viewModel.GenerateButtonText.Should().NotBe(initialText);
        viewModel.GenerateButtonText.Should().Contain("USD");
    }

    private static void SimulateBindingsClearingSelectionsAfterOptionSourcesRefresh(
        UniversalNanoBananaPanelViewModel viewModel)
    {
        viewModel.PropertyChanged += (_, args) =>
        {
            if (string.Equals(
                    args.PropertyName,
                    nameof(UniversalNanoBananaPanelViewModel.AspectRatios),
                    StringComparison.Ordinal))
            {
                viewModel.SelectedAspectRatio = string.Empty;
            }

            if (string.Equals(
                    args.PropertyName,
                    nameof(UniversalNanoBananaPanelViewModel.Resolutions),
                    StringComparison.Ordinal))
            {
                viewModel.SelectedResolution = string.Empty;
            }

            if (string.Equals(
                    args.PropertyName,
                    nameof(UniversalNanoBananaPanelViewModel.GenerationCounts),
                    StringComparison.Ordinal))
            {
                viewModel.GenerationCount = 0;
            }
        };
    }

    private static UniversalNanoBananaPanelViewModel CreateViewModel(
        IImageGenerationApiClient? apiClient = null,
        IGenerationLifecycleEventHub? lifecycleEventHub = null,
        ISecretStore? secretStore = null,
        IGenerationModelCatalogApiClient? catalogApiClient = null,
        IImageModelOptionCatalog? imageModelOptionCatalog = null,
        IViewModelErrorHandler? errorHandler = null,
        IGenerationRunDispatcher? dispatcher = null,
        IGenerationPanelStateService? generationPanelStateService = null,
        IPanelAttachmentStore? attachmentStore = null,
        IImageViewerService? imageViewerService = null,
        IAttachedImagePreparationService? attachmentPreparationService = null,
        IApiEndpointService? apiEndpointService = null,
        IUiThreadDispatcher? uiThreadDispatcher = null,
        bool initializeCatalog = true)
    {
        IImageGenerationApiClient generationApiClient = apiClient ?? new SuccessfulImageGenerationApiClient();
        IGenerationLifecycleEventHub generationLifecycleEventHub =
            lifecycleEventHub ?? new TestGenerationLifecycleEventHub();
        INanoBanana2AttachmentValidator attachmentValidator = CreateAttachmentValidator();
        IViewModelErrorHandler viewModelErrorHandler = errorHandler ?? new TestViewModelErrorHandler();
        IGenerationRunDispatcher generationRunDispatcher = dispatcher
            ?? GenerationRunDispatcherTestFactory.Create(
                generationApiClient,
                generationLifecycleEventHub);

        UniversalNanoBananaPanelViewModel viewModel = new(
            new EmptyFilePickerService(),
            secretStore ?? new RecordingSecretStore(TestGenerationCredentials.ProviderCredential),
            catalogApiClient ?? new SuccessfulGenerationModelCatalogApiClient(),
            imageModelOptionCatalog ?? CreateImageModelOptionCatalog(initializeCatalog),
            apiEndpointService ?? TestApiEndpointServiceFactory.Create(),
            uiThreadDispatcher ?? new ImmediateUiThreadDispatcher(),
            new UniversalNanoBananaPanelModelScope(),
            new NanoBanana2AttachmentsViewModel(
                attachmentValidator,
                attachmentPreparationService ?? new PassThroughAttachedImagePreparationService(),
                attachmentStore ?? new InMemoryPanelAttachmentStore()),
            new NanoBanana2GenerationRunner(
                new NanoBanana2GenerationRequestBuilder(),
                generationRunDispatcher),
            generationPanelStateService ?? new RecordingGenerationPanelStateService(),
            imageViewerService ?? new RecordingImageViewerService(),
            new NanoBanana2QuoteViewModel(new GenerationPricePreviewEstimator()),
            viewModelErrorHandler);

        return viewModel;
    }

    private static RecordingGenerationPanelStateService CreateStateService(
        GenerationModelMetadataDto metadata,
        string aspectRatio,
        string resolution,
        int generationCount,
        string prompt,
        double? temperature = null)
    {
        return new RecordingGenerationPanelStateService
        {
            StateToLoad = new GenerationPanelState
            {
                PanelId = GenerationPanelIds.NanoBanana,
                SelectedModelId = metadata.Id,
                AspectRatio = aspectRatio,
                Resolution = resolution,
                Temperature = temperature,
                GenerationCount = generationCount,
                Prompt = prompt
            }
        };
    }

    private static INanoBanana2AttachmentValidator CreateAttachmentValidator()
    {
        return new NanoBanana2AttachmentValidator(new AttachedImageSignatureValidator());
    }

    private static void SetApiBaseAddress(
        IApiEndpointService endpointService,
        string value)
    {
        ApiBaseAddress.TryCreate(value, out ApiBaseAddress? baseAddress).Should().BeTrue();
        endpointService.SetBaseAddress(baseAddress
            ?? throw new InvalidOperationException("API base address is required."));
    }

    private static IImageModelOptionCatalog CreateImageModelOptionCatalog(bool initializeCatalog)
    {
        ImageModelOptionCatalog catalog = new();

        if (initializeCatalog)
        {
            catalog.Initialize(ApiModelMetadataTestCatalog.LoadCatalog());
        }

        return catalog;
    }

    private static IImageModelOptionCatalog CreateImageModelOptionCatalog(GenerationModelCatalogDto catalogDto)
    {
        ImageModelOptionCatalog catalog = new();
        catalog.Initialize(catalogDto);

        return catalog;
    }

    private static GenerationModelCatalogDto CreateTestModelCatalog()
    {
        return TestGenerationModelCatalogAugmenter.AddTestModelIfEnabled(
            ApiModelMetadataTestCatalog.LoadCatalog(),
            new TestGenerationOptions
            {
                Enabled = true
            });
    }

    private static AttachedImageDto CreateAttachedImage(string fileName)
    {
        return new AttachedImageDto(fileName, GenerationImageContentTypes.Png, PngBytes);
    }

    private static AttachedImageDto CreateLargeAttachedImage(string fileName, int length)
    {
        byte[] content = new byte[length];
        PngBytes.CopyTo(content, 0);

        return new AttachedImageDto(fileName, GenerationImageContentTypes.Png, content);
    }

    private static string GetAttachedSourceFileName(GalleryImageViewerItem item)
    {
        item.Source.Should().BeOfType<GalleryAttachedImageViewerSource>();

        return ((GalleryAttachedImageViewerSource)item.Source).Image.FileName;
    }

    private static IReadOnlyList<AttachedImageDto> CreateAttachedImagesWithNull()
    {
        IReadOnlyList<AttachedImageDto>? images = JsonSerializer.Deserialize<IReadOnlyList<AttachedImageDto>>(
            "[null]",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return images ?? throw new InvalidOperationException("Attached images test payload must be deserialized.");
    }

    private static GenerationModelCatalogDto CreateCatalogWithOtherModel()
    {
        return new GenerationModelCatalogDto(
        [
            new(
                    "other-model",
                    "Other Model",
                    GenerationProviderIds.Google,
                    "provider-other-model",
                    "other-panel",
                    1000,
                    500,
                    100,
                    [GenerationAspectRatios.Auto],
                    ["1k"],
                    [1],
                    new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
                    new GenerationModelAttachmentMetadataDto(
                        1,
                        1024,
                        2048,
                        [GenerationImageContentTypes.Png]),
                    GenerationModelPricingMetadataTestFactory.CreateCatalogPricing(
                        new Dictionary<string, int>
                        {
                            ["1k"] = 1120
                        }))
        ]);
    }

    private static GenerationModelCatalogDto CreateCompatibilityCatalog(
        IReadOnlyList<string> firstAspectRatios,
        IReadOnlyList<string> firstResolutions,
        IReadOnlyList<int> firstGenerationCounts,
        IReadOnlyList<string> secondAspectRatios,
        IReadOnlyList<string> secondResolutions,
        IReadOnlyList<int> secondGenerationCounts)
    {
        return new GenerationModelCatalogDto(
        [
            CreateCompatibilityModelMetadata(
                    "compat-model-a",
                    "A Compatibility Model",
                    firstAspectRatios,
                    firstResolutions,
                    firstGenerationCounts),
                CreateCompatibilityModelMetadata(
                    "compat-model-b",
                    "B Compatibility Model",
                    secondAspectRatios,
                    secondResolutions,
                    secondGenerationCounts)
        ]);
    }

    private static GenerationModelMetadataDto CreateCompatibilityModelMetadata(
        string id,
        string displayName,
        IReadOnlyList<string> aspectRatios,
        IReadOnlyList<string> resolutions,
        IReadOnlyList<int> generationCounts)
    {
        return new GenerationModelMetadataDto(
            id,
            displayName,
            GenerationProviderIds.Google,
            string.Concat(id, "-provider"),
            GenerationPanelIds.NanoBanana,
            1000,
            500,
            1000,
            aspectRatios,
            resolutions,
            generationCounts,
            new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
            new GenerationModelAttachmentMetadataDto(
                1,
                1024,
                2048,
                [GenerationImageContentTypes.Png]),
            CreateCompatibilityPricing(resolutions));
    }

    private static GenerationModelPricingMetadataDto CreateCompatibilityPricing(
        IReadOnlyList<string> resolutions)
    {
        Dictionary<string, int> outputImageTokensByResolution =
            new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string resolution in resolutions)
        {
            outputImageTokensByResolution[resolution] = 1120;
        }

        return GenerationModelPricingMetadataTestFactory.CreateCatalogPricing(
            outputImageTokensByResolution);
    }

    private static GenerationModelCatalogDto CreateSmallLimitCatalog()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();

        return new GenerationModelCatalogDto(
        [
            metadata with
                {
                    Attachments = new GenerationModelAttachmentMetadataDto(
                        10,
                        1024,
                        4096,
                        metadata.Attachments.SupportedContentTypes)
                }
        ]);
    }

    private static async Task FillAttachedImagesToLimitAsync(UniversalNanoBananaPanelViewModel viewModel)
    {
        await AttachValidImagesAsync(viewModel, GetSelectedModel(viewModel).MaxAttachedImages);
    }

    private static ImageModelOption GetModel(
        UniversalNanoBananaPanelViewModel viewModel,
        string modelId)
    {
        return viewModel.AvailableModels.Single(model =>
            string.Equals(model.Id, modelId, StringComparison.Ordinal));
    }

    private static async Task AttachValidImagesAsync(UniversalNanoBananaPanelViewModel viewModel, int count)
    {
        IReadOnlyList<AttachedImageDto> images = Enumerable
            .Range(0, count)
            .Select(index => CreateAttachedImage($"image-{index}.png"))
            .ToList();

        await viewModel.AttachImagesCommand.ExecuteAsync(images);
    }

    private static async Task<AttachedImageViewModel> AttachImageAsync(
        UniversalNanoBananaPanelViewModel viewModel,
        string fileName)
    {
        IReadOnlyList<AttachedImageDto> images = [CreateAttachedImage(fileName)];

        await viewModel.AttachImagesCommand.ExecuteAsync(images);

        return viewModel.AttachedImages.Single(image =>
            string.Equals(image.FileName, fileName, StringComparison.Ordinal));
    }

    private static async Task AssertImageAttachmentRejectedAsync(IReadOnlyList<AttachedImageDto> images)
    {
        UniversalNanoBananaPanelViewModel viewModel = CreateViewModel();

        await viewModel.AttachImagesCommand.ExecuteAsync(images);

        viewModel.AttachedImages.Should().BeEmpty();
        viewModel.ErrorMessage.Should().Be(UiStrings.ImageAttachmentFailed);
    }

    private sealed class SelectionValueResetRecorder
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        public SelectionValueResetRecorder(UniversalNanoBananaPanelViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            viewModel.SelectionValueReset += OnSelectionValueReset;
        }

        public int CountFor(string propertyName)
        {
            return _counts.GetValueOrDefault(propertyName);
        }

        public void AssertCounts(
            int aspectRatioCount,
            int resolutionCount,
            int generationCount)
        {
            CountFor(nameof(UniversalNanoBananaPanelViewModel.SelectedAspectRatio)).Should()
                .Be(aspectRatioCount);
            CountFor(nameof(UniversalNanoBananaPanelViewModel.SelectedResolution)).Should()
                .Be(resolutionCount);
            CountFor(nameof(UniversalNanoBananaPanelViewModel.GenerationCount)).Should()
                .Be(generationCount);
        }

        private void OnSelectionValueReset(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName))
            {
                return;
            }

            _counts[e.PropertyName] = CountFor(e.PropertyName) + 1;
        }
    }

    private sealed class RecordingGenerationPanelStateService : IGenerationPanelStateService
    {
        public List<GenerationPanelState> SavedStates { get; } = [];
        public int LoadCallCount { get; private set; }
        public GenerationPanelState StateToLoad { get; set; } = new()
        {
            PanelId = GenerationPanelIds.NanoBanana
        };

        public Task<GenerationPanelState> LoadAsync(string panelId, CancellationToken ct)
        {
            LoadCallCount++;

            return Task.FromResult(StateToLoad);
        }

        public Task SaveAsync(string panelId, GenerationPanelState state, CancellationToken ct)
        {
            SavedStates.Add(state);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingGenerationPanelStateService : IGenerationPanelStateService
    {
        private readonly string _message;

        public ThrowingGenerationPanelStateService(string message)
        {
            _message = message;
        }

        public Task<GenerationPanelState> LoadAsync(string panelId, CancellationToken ct)
        {
            throw new InvalidOperationException(_message);
        }

        public Task SaveAsync(string panelId, GenerationPanelState state, CancellationToken ct)
        {
            throw new NotSupportedException("State saving is not used by this test.");
        }
    }

    private abstract class PanelAttachmentStoreBase : IPanelAttachmentStore
    {
        private readonly Dictionary<string, AttachedImageDto> _images = new(StringComparer.Ordinal);

        public abstract PanelAttachmentState CreateState(AttachedImageDto image);

        public Task SaveAsync(
            string panelId,
            PanelAttachmentState attachment,
            AttachedImageDto image,
            CancellationToken ct)
        {
            OnSaving();
            _images[attachment.InternalFileName] = CreateStoredImage(attachment, image);

            return Task.CompletedTask;
        }

        public Task<AttachedImageDto?> LoadAsync(
            string panelId,
            PanelAttachmentState attachment,
            CancellationToken ct)
        {
            _images.TryGetValue(attachment.InternalFileName, out AttachedImageDto? image);

            return Task.FromResult(image);
        }

        public Task DeleteAsync(
            string panelId,
            PanelAttachmentState attachment,
            CancellationToken ct)
        {
            OnDeleting();
            _images.Remove(attachment.InternalFileName);

            return Task.CompletedTask;
        }

        protected virtual AttachedImageDto CreateStoredImage(
            PanelAttachmentState attachment,
            AttachedImageDto image)
        {
            return image;
        }

        protected virtual void OnSaving()
        {
        }

        protected virtual void OnDeleting()
        {
        }
    }

    private sealed class InMemoryPanelAttachmentStore : PanelAttachmentStoreBase
    {
        public override PanelAttachmentState CreateState(AttachedImageDto image)
        {
            string id = Guid.NewGuid().ToString("N");
            string internalFileName = string.Concat(id, ".managed");
            AttachedImageDto managedImage = new(
                Path.GetFileName(image.FileName),
                image.ContentType,
                image.Content);

            return new PanelAttachmentState
            {
                Id = id,
                FileName = managedImage.FileName,
                ContentType = managedImage.ContentType,
                SizeBytes = managedImage.Content.LongLength,
                InternalFileName = internalFileName
            };
        }

        protected override AttachedImageDto CreateStoredImage(
            PanelAttachmentState attachment,
            AttachedImageDto image)
        {
            return new AttachedImageDto(
                attachment.FileName,
                attachment.ContentType,
                image.Content);
        }
    }

    private sealed class RecordingPanelAttachmentStore : PanelAttachmentStoreBase
    {
        public int SaveCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }

        private int _nextId;

        public override PanelAttachmentState CreateState(AttachedImageDto image)
        {
            _nextId++;
            string id = string.Concat("attachment-", _nextId.ToString("D2"));
            string internalFileName = string.Concat(id, ".png");

            return new PanelAttachmentState
            {
                Id = id,
                FileName = Path.GetFileName(image.FileName),
                ContentType = image.ContentType,
                SizeBytes = image.Content.LongLength,
                InternalFileName = internalFileName
            };
        }

        protected override void OnSaving()
        {
            SaveCallCount++;
        }

        protected override void OnDeleting()
        {
            DeleteCallCount++;
        }
    }

    private sealed class RecordingSecretStore : ISecretStore
    {
        private readonly string? _value;
        public int GetCallCount { get; private set; }

        public RecordingSecretStore(string? value)
        {
            _value = value;
        }

        public Task<string?> GetSecretAsync(string key, CancellationToken ct)
        {
            key.Should().Be(GoogleApiKeySettingDefinition.SecretNameValue);
            GetCallCount++;

            return Task.FromResult(_value);
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct)
        {
            throw new NotSupportedException("Panel tests do not write secrets.");
        }
    }

    private sealed class CapturingGenerationRunDispatcher : IGenerationRunDispatcher
    {
        public IReadOnlyList<GenerationRunRequest> CapturedRequests => _capturedRequests;
        public GenerationRunRequest? CapturedRequest => _capturedRequests.LastOrDefault();
        public CancellationToken CapturedCancellationToken { get; private set; }

        private readonly List<GenerationRunRequest> _capturedRequests = [];

        public Task EnqueueAsync(GenerationRunRequest request, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(request);

            _capturedRequests.Add(request);
            CapturedCancellationToken = ct;

            return Task.CompletedTask;
        }
    }

    private sealed class DelayedGenerationModelCatalogApiClient : IGenerationModelCatalogApiClient
    {
        private readonly TaskCompletionSource _requestReceived = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<GenerationModelCatalogDto> _response = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestReceivedTask => _requestReceived.Task;

        public Task<GenerationModelCatalogDto> GetCatalogAsync(CancellationToken ct = default)
        {
            _requestReceived.TrySetResult();

            return _response.Task.WaitAsync(ct);
        }

        public void Complete()
        {
            _response.TrySetResult(ApiModelMetadataTestCatalog.LoadCatalog());
        }
    }

    private sealed class SuccessfulGenerationModelCatalogApiClient : IGenerationModelCatalogApiClient
    {
        public int CallCount { get; private set; }

        private readonly GenerationModelCatalogDto _catalog;

        public SuccessfulGenerationModelCatalogApiClient()
            : this(ApiModelMetadataTestCatalog.LoadCatalog())
        {
        }

        public SuccessfulGenerationModelCatalogApiClient(GenerationModelCatalogDto catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            _catalog = catalog;
        }

        public Task<GenerationModelCatalogDto> GetCatalogAsync(CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(_catalog);
        }
    }

    private sealed class SequencedGenerationModelCatalogApiClient : IGenerationModelCatalogApiClient
    {
        public int RequestCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _requestCount;
                }
            }
        }

        public int ReturnedResponseCount => Volatile.Read(ref _returnedResponseCount);

        private readonly object _syncRoot = new();
        private readonly TaskCompletionSource<GenerationModelCatalogDto>[] _responses =
        [
            new(
                TaskCreationOptions.RunContinuationsAsynchronously),
            new(
                TaskCreationOptions.RunContinuationsAsynchronously)
        ];
        private int _requestCount;
        private int _returnedResponseCount;

        public async Task<GenerationModelCatalogDto> GetCatalogAsync(
            CancellationToken ct = default)
        {
            Task<GenerationModelCatalogDto> responseTask;

            lock (_syncRoot)
            {
                if (_requestCount >= _responses.Length)
                {
                    throw new InvalidOperationException("Unexpected catalog request.");
                }

                responseTask = _responses[_requestCount].Task;
                _requestCount++;
            }

            GenerationModelCatalogDto response = await responseTask;
            Interlocked.Increment(ref _returnedResponseCount);
            return response;
        }

        public void Complete(int index, GenerationModelCatalogDto catalog)
        {
            _responses[index].TrySetResult(catalog);
        }
    }

    private sealed class ThrowingGenerationModelCatalogApiClient : IGenerationModelCatalogApiClient
    {
        public Task<GenerationModelCatalogDto> GetCatalogAsync(CancellationToken ct = default)
        {
            throw new HttpRequestException("Catalog API failed.");
        }
    }
}

