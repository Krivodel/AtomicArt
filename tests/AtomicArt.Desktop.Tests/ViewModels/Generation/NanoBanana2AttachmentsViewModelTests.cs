using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Generation.State;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.ViewModels.Generation;

namespace AtomicArt.Desktop.Tests.ViewModels.Generation;

public sealed class NanoBanana2AttachmentsViewModelTests
{
    private const string PanelId = "panel";

    [Fact]
    public async Task AttachInputsAsync_WithExistingImages_AppendsNewImagesToEnd()
    {
        NanoBanana2AttachmentsViewModel viewModel = CreateViewModel();
        ImageModelOption selectedModel = CreateModel();
        AttachedImageDto[] firstImages =
        [
            GenerationImageTestData.CreateAttachedImage("first.png")
        ];
        AttachedImageDto[] secondImages =
        [
            GenerationImageTestData.CreateAttachedImage("second.png"),
            GenerationImageTestData.CreateAttachedImage("third.png")
        ];

        await AttachImagesAsync(viewModel, selectedModel, firstImages);
        await AttachImagesAsync(viewModel, selectedModel, secondImages);

        viewModel.AttachedImages.Select(image => image.FileName)
            .Should()
            .Equal("first.png", "second.png", "third.png");
        viewModel.GetAttachedImageDtos()
            .Select(image => image.FileName)
            .Should()
            .Equal("first.png", "second.png", "third.png");
    }

    [Fact]
    public async Task MoveAttachment_WhenTargetIndexChanges_ReordersAttachedImagesAndDtos()
    {
        NanoBanana2AttachmentsViewModel viewModel = CreateViewModel();
        ImageModelOption selectedModel = CreateModel();
        AttachedImageDto[] images =
        [
            GenerationImageTestData.CreateAttachedImage("first.png"),
            GenerationImageTestData.CreateAttachedImage("second.png"),
            GenerationImageTestData.CreateAttachedImage("third.png")
        ];

        await AttachImagesAsync(viewModel, selectedModel, images);
        AttachedImageViewModel thirdImage = viewModel.AttachedImages[2];

        viewModel.MoveAttachment(thirdImage, 0);

        viewModel.AttachedImages.Select(image => image.FileName)
            .Should()
            .Equal("third.png", "first.png", "second.png");
        viewModel.GetAttachedImageDtos()
            .Select(image => image.FileName)
            .Should()
            .Equal("third.png", "first.png", "second.png");
    }

    [Fact]
    public async Task MoveAttachment_WhilePreparationRuns_ReordersPendingImages()
    {
        AttachedImageDto[] images =
        [
            GenerationImageTestData.CreateAttachedImage("first.png"),
            GenerationImageTestData.CreateAttachedImage("second.png")
        ];
        PendingAttachmentScenario scenario = new(images);
        await scenario.WaitUntilStartedAsync();
        AttachedImageViewModel secondImage = scenario.ViewModel.AttachedImages[1];

        scenario.ViewModel.MoveAttachment(secondImage, 0);

        scenario.ViewModel.AttachedImages.Select(image => image.FileName)
            .Should()
            .Equal("second.png", "first.png");

        foreach (AttachedImageViewModel image in scenario.ViewModel.AttachedImages.ToList())
        {
            await scenario.ViewModel.RemoveAttachmentAsync(
                PanelId,
                image,
                CancellationToken.None);
        }

        await scenario.Attaching;
    }

    [Fact]
    public async Task RemoveAttachmentAsync_WhilePreparationRuns_CancelsAndRemovesImmediately()
    {
        AttachedImageDto[] images =
        [
            GenerationImageTestData.CreateAttachedImage("pending.png")
        ];
        PendingAttachmentScenario scenario = new(images);
        await scenario.WaitUntilStartedAsync();
        AttachedImageViewModel pendingImage = scenario.ViewModel.AttachedImages.Single();

        await scenario.ViewModel.RemoveAttachmentAsync(
            PanelId,
            pendingImage,
            CancellationToken.None);

        scenario.ViewModel.AttachedImages.Should().BeEmpty();
        scenario.ViewModel.HasPendingAttachments.Should().BeFalse();
        await scenario.Attaching;
    }

    [Fact]
    public async Task AttachInputsAsync_WhileSourceIsReading_ShowsCancelablePlaceholder()
    {
        TaskCompletionSource sourceReadStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        NanoBanana2AttachmentsViewModel viewModel = CreateViewModel();
        ImageModelOption selectedModel = CreateModel();
        ImageAttachmentInput input = new(
            "reading.png",
            async ct =>
            {
                sourceReadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);

                return GenerationImageTestData.CreateAttachedImage("reading.png");
            });
        ImageAttachmentInput[] inputs = [input];

        Task attaching = viewModel.AttachInputsAsync(
            PanelId,
            selectedModel,
            inputs,
            CancellationToken.None);
        await sourceReadStarted.Task;
        AttachedImageViewModel pendingImage = viewModel.AttachedImages.Single();

        pendingImage.IsLoading.Should().BeTrue();
        await viewModel.RemoveAttachmentAsync(
            PanelId,
            pendingImage,
            CancellationToken.None);

        viewModel.AttachedImages.Should().BeEmpty();
        await attaching;
    }

    [Fact]
    public async Task AttachInputsAsync_WhenPreparationThrows_RemovesPlaceholderAndReportsFailure()
    {
        NanoBanana2AttachmentsViewModel viewModel = CreateViewModel(
            new ThrowingAttachedImagePreparationService());
        AttachmentStateChangedEventArgs? failure = null;
        viewModel.AttachmentStateChanged += (_, eventArgs) =>
        {
            if (eventArgs.Kind == AttachmentStateChangeKind.Failed)
            {
                failure = eventArgs;
            }
        };
        ImageModelOption selectedModel = CreateModel();
        AttachedImageDto[] images =
        [
            GenerationImageTestData.CreateAttachedImage("failed.png")
        ];

        await AttachImagesAsync(viewModel, selectedModel, images);

        viewModel.AttachedImages.Should().BeEmpty();
        viewModel.HasPendingAttachments.Should().BeFalse();
        failure.Should().NotBeNull();
        failure?.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task AttachInputsAsync_WhenOnePreparationFails_CompletesOtherAttachment()
    {
        const string rejectedFileName = "rejected.png";
        NanoBanana2AttachmentsViewModel viewModel = CreateViewModel(
            new SelectiveAttachedImagePreparationService(rejectedFileName));
        List<AttachmentStateChangeKind> changes = [];
        viewModel.AttachmentStateChanged += (_, eventArgs) => changes.Add(eventArgs.Kind);
        ImageModelOption selectedModel = CreateModel();
        AttachedImageDto[] images =
        [
            GenerationImageTestData.CreateAttachedImage(rejectedFileName),
            GenerationImageTestData.CreateAttachedImage("accepted.png")
        ];

        await AttachImagesAsync(viewModel, selectedModel, images);

        viewModel.AttachedImages.Should().ContainSingle();
        viewModel.AttachedImages.Single().FileName.Should().Be("accepted.png");
        changes.Should().Contain(AttachmentStateChangeKind.Failed);
        changes.Should().Contain(AttachmentStateChangeKind.Completed);
    }

    [Fact]
    public async Task AttachInputsAsync_FromSeparateCalls_CompletesEachAttachmentIndependently()
    {
        AttachedImageDto firstImage = GenerationImageTestData.CreateAttachedImage("first.png");
        AttachedImageDto secondImage = GenerationImageTestData.CreateAttachedImage("second.png");
        ControlledAttachedImagePreparationService preparationService = new(
            [firstImage.FileName, secondImage.FileName]);
        NanoBanana2AttachmentsViewModel viewModel = CreateViewModel(preparationService);
        ImageModelOption selectedModel = CreateModel();

        Task firstAttachment = AttachImagesAsync(
            viewModel,
            selectedModel,
            [firstImage]);
        await preparationService.WaitUntilStartedAsync(firstImage.FileName);
        Task secondAttachment = AttachImagesAsync(
            viewModel,
            selectedModel,
            [secondImage]);
        await preparationService.WaitUntilStartedAsync(secondImage.FileName);
        preparationService.Complete(secondImage);
        await secondAttachment;

        viewModel.AttachedImages[0].IsLoading.Should().BeTrue();
        viewModel.AttachedImages[1].IsReady.Should().BeTrue();
        viewModel.HasPendingAttachments.Should().BeTrue();

        preparationService.Complete(firstImage);
        await firstAttachment;

        viewModel.AttachedImages.Should().OnlyContain(image => image.IsReady);
        viewModel.HasPendingAttachments.Should().BeFalse();
    }

    private static NanoBanana2AttachmentsViewModel CreateViewModel()
    {
        return CreateViewModel(new PassThroughAttachedImagePreparationService());
    }

    private static NanoBanana2AttachmentsViewModel CreateViewModel(
        IAttachedImagePreparationService preparationService)
    {
        return new NanoBanana2AttachmentsViewModel(
            new PassThroughAttachmentValidator(),
            preparationService,
            new InMemoryPanelAttachmentStore());
    }

    private static ImageModelOption CreateModel()
    {
        return new ImageModelOption(
            "model",
            "Model",
            "provider",
            "provider-model",
            "panel",
            1024,
            1024,
            new List<string> { "1:1" },
            new List<string> { "1K" },
            new List<int> { 1 },
            new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d),
            8,
            1024 * 1024,
            8L * 1024L * 1024L,
            new List<string> { GenerationImageContentTypes.Png },
            GenerationModelPricingMetadataTestFactory.CreateFreePricing());
    }

    private static Task AttachImagesAsync(
        NanoBanana2AttachmentsViewModel viewModel,
        ImageModelOption selectedModel,
        IReadOnlyList<AttachedImageDto> images)
    {
        List<ImageAttachmentInput> inputs = images
            .Select(ImageAttachmentInput.FromImage)
            .ToList();

        return viewModel.AttachInputsAsync(
            PanelId,
            selectedModel,
            inputs,
            CancellationToken.None);
    }

    private sealed class PendingAttachmentScenario
    {
        public NanoBanana2AttachmentsViewModel ViewModel { get; }
        public Task Attaching { get; }

        private readonly BlockingAttachedImagePreparationService _preparationService;

        public PendingAttachmentScenario(IReadOnlyList<AttachedImageDto> images)
        {
            _preparationService = new BlockingAttachedImagePreparationService();
            ViewModel = CreateViewModel(_preparationService);
            Attaching = AttachImagesAsync(ViewModel, CreateModel(), images);
        }

        public Task WaitUntilStartedAsync()
        {
            return _preparationService.WaitUntilStartedAsync();
        }
    }

    private sealed class PassThroughAttachmentValidator : INanoBanana2AttachmentValidator
    {
        public IReadOnlyList<AttachedImageDto> CreateValidatedAttachments(
            ImageModelOption selectedModel,
            IReadOnlyList<AttachedImageDto> currentImages,
            IReadOnlyList<AttachedImageDto>? incomingImages)
        {
            _ = selectedModel;

            if (incomingImages is null)
            {
                return currentImages;
            }

            return currentImages
                .Concat(incomingImages)
                .ToList();
        }
    }

    private sealed class InMemoryPanelAttachmentStore : IPanelAttachmentStore
    {
        public PanelAttachmentState CreateState(AttachedImageDto image)
        {
            return new PanelAttachmentState
            {
                Id = image.FileName,
                FileName = image.FileName,
                ContentType = image.ContentType,
                SizeBytes = image.Content.LongLength,
                InternalFileName = image.FileName
            };
        }

        public Task SaveAsync(
            string panelId,
            PanelAttachmentState attachment,
            AttachedImageDto image,
            CancellationToken ct)
        {
            _ = panelId;
            _ = attachment;
            _ = image;
            _ = ct;

            return Task.CompletedTask;
        }

        public Task<AttachedImageDto?> LoadAsync(
            string panelId,
            PanelAttachmentState attachment,
            CancellationToken ct)
        {
            _ = panelId;
            _ = attachment;
            _ = ct;

            return Task.FromResult<AttachedImageDto?>(null);
        }

        public Task DeleteAsync(
            string panelId,
            PanelAttachmentState attachment,
            CancellationToken ct)
        {
            _ = panelId;
            _ = attachment;
            _ = ct;

            return Task.CompletedTask;
        }
    }
}
