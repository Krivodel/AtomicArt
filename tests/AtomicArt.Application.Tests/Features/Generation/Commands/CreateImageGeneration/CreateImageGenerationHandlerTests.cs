using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Application.Tests.Generation;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

namespace AtomicArt.Application.Tests.Features.Generation.Commands.CreateImageGeneration;

public sealed class CreateImageGenerationHandlerTests
{
    private const int PngSignatureLength = 8;

    private static readonly Guid ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan AsyncAssertionTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsBatchWithImageContent()
    {
        Guid plannedBatchId = Guid.Empty;
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock(captureBatchId: batchId => plannedBatchId = batchId);
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand();

        GenerationBatchDto batch = await HandleSuccessfullyAsync(handler, command);

        batch.BatchId.Should().Be(plannedBatchId);
        plannedBatchId.Should().NotBe(Guid.Empty);
        GenerationItemDto item = batch.Items.Single();
        item.ImagePath.Should().BeNull();
        item.ImageContent.Should().NotBeNull();
        item.ImageContent.Should().BeEquivalentTo(new GenerationImageContentDto("image/png", "iVBORw0KGgo="));
        item.CompletedAtUtc.Should().NotBeNull();
        item.GenerationDuration.Should().NotBeNull();
        item.Price.Should().BeNull();
        item.Usage.Should().BeNull();
        outputPlanner.Verify(
            planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                plannedBatchId,
                ApiModelMetadataTestCatalog.NanoBanana2DisplayName),
            Times.Once);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.Is<ImageGenerationContentProviderContext>(context =>
                    context.ItemIndex == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithProviderUsage_AddsPriceAndDurationToItem()
    {
        DateTime completedAtUtc = CreatedAtUtc.AddSeconds(30);
        TimeSpan generationDuration = TimeSpan.FromSeconds(30);
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();
        ImageGenerationContentResult content = new(
            "image/png",
            "iVBORw0KGgo=",
            usage,
            new GenerationPriceDto(
                0.0678m,
                "USD",
                GenerationPriceSources.ActualProviderUsage),
            completedAtUtc,
            generationDuration);
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock(content: content);
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand();

        GenerationBatchDto batch = await HandleSuccessfullyAsync(handler, command);

        GenerationItemDto item = batch.Items.Single();
        item.CompletedAtUtc.Should().Be(completedAtUtc);
        item.GenerationDuration.Should().Be(generationDuration);
        item.Usage.Should().BeSameAs(usage);
        item.Price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public async Task Handle_WithNegativeProviderDuration_ClampsDurationToZero()
    {
        ImageGenerationContentResult content = new(
            "image/png",
            "iVBORw0KGgo=",
            GenerationDuration: TimeSpan.FromSeconds(-1));
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock(content: content);
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand();

        GenerationBatchDto batch = await HandleSuccessfullyAsync(handler, command);

        batch.Items.Single().GenerationDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Handle_WithProviderCredential_PassesProviderContextToContentProvider()
    {
        ImageGenerationContentProviderContext? capturedContext = null;
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock(
            captureContext: context => capturedContext = context);
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand(
            providerCredential: TestGenerationCredentials.ProviderCredential);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ImageGenerationContentProviderContext context = capturedContext
            ?? throw new InvalidOperationException("Provider context is missing.");
        context.ProviderCredential.Should().Be(TestGenerationCredentials.ProviderCredential);
        context.Provider.Should().Be(GenerationProviderIds.Google);
        context.ProviderModelId.Should().Be(ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().ProviderModelId);
        context.ItemIndex.Should().Be(0);
        context.Request.ModelId.Should().Be(command.Request.ModelId);
    }

    [Fact]
    public async Task Handle_WithNormalizableAttachment_PassesValidatedRequestToPlannerAndWriter()
    {
        ImageGenerationRequestDto? plannedRequest = null;
        ImageGenerationRequestDto? contentRequest = null;
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock(
            captureRequest: request => plannedRequest = request);
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock(
            captureRequest: request => contentRequest = request);
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new(
                    " source.png ",
                    " IMAGE/PNG ",
                    CreatePngContent())
            ]);

        await HandleSuccessfullyAsync(handler, command);

        ImageGenerationRequestDto capturedPlannedRequest = plannedRequest
            ?? throw new InvalidOperationException("Planned request is missing.");
        ImageGenerationRequestDto capturedContentRequest = contentRequest
            ?? throw new InvalidOperationException("Content request is missing.");
        AttachedImageDto plannedImage = capturedPlannedRequest.AttachedImages.Single();
        AttachedImageDto contentImage = capturedContentRequest.AttachedImages.Single();
        plannedImage.FileName.Should().Be("source.png");
        plannedImage.ContentType.Should().Be("image/png");
        contentImage.FileName.Should().Be("source.png");
        contentImage.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task Handle_WithGenerationCountTwo_ReturnsTwoContentItems()
    {
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMockForRequestCount();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand(generationCount: 2);

        GenerationBatchDto batch = await HandleSuccessfullyAsync(handler, command);

        batch.Items.Should().HaveCount(2);

        foreach (GenerationItemDto item in batch.Items)
        {
            item.ImagePath.Should().BeNull();
            item.ImageContent.Should().NotBeNull();
        }

        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.Is<ImageGenerationContentProviderContext>(context =>
                    context.ItemIndex == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.Is<ImageGenerationContentProviderContext>(context =>
                    context.ItemIndex == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleGenerationItems_StartsContentRequestsInParallel()
    {
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMockForRequestCount();
        ParallelTrackingContentProvider contentProvider = new(expectedRequestCount: 2);
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider);
        CreateImageGenerationCommand command = CreateCommand(generationCount: 2);

        Task<Result<GenerationBatchDto>> handleTask = handler.Handle(command, CancellationToken.None);

        try
        {
            await contentProvider.AllRequestsStarted.WaitAsync(AsyncAssertionTimeout);
            handleTask.IsCompleted.Should().BeFalse();
        }
        finally
        {
            contentProvider.ReleaseRequests();
        }

        Result<GenerationBatchDto> result = await handleTask.WaitAsync(AsyncAssertionTimeout);

        result.IsSuccess.Should().BeTrue();
        contentProvider.StartedCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithUnsupportedGenerationCount_ReturnsValidationError()
    {
        HandlerTestContext context = new();
        CreateImageGenerationCommand command = CreateCommand(generationCount: 5);

        Result<GenerationBatchDto> result = await context.Handler.Handle(command, CancellationToken.None);

        context.AssertValidationRejected(result);
    }

    [Fact]
    public async Task Handle_WithTooManyAttachedImages_ReturnsValidationError()
    {
        HandlerTestContext context = new();
        IReadOnlyList<AttachedImageDto> attachedImages = Enumerable
            .Range(0, 15)
            .Select(index => new AttachedImageDto(
                $"reference-{index}.png",
                "image/png",
                CreatePngContent()))
            .ToList();
        CreateImageGenerationCommand command = CreateCommand(attachedImages: attachedImages);

        Result<GenerationBatchDto> result = await context.Handler.Handle(command, CancellationToken.None);

        context.AssertValidationRejected(result);
    }

    [Fact]
    public async Task Handle_WithOversizedAttachedImage_ReturnsValidationError()
    {
        GenerationModelMetadataDto metadata = CreateMetadataWithAttachmentLimits(
            maxCount: 3,
            maxSingleFileBytes: PngSignatureLength,
            maxTotalBytes: PngSignatureLength * 3L);
        HandlerTestContext context = new(metadata);
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto("reference.png", "image/png", CreateLargePngContent(PngSignatureLength + 1))
            ]);

        Result<GenerationBatchDto> result = await context.Handler.Handle(command, CancellationToken.None);

        context.AssertValidationRejected(result);
    }

    [Fact]
    public async Task Handle_WithExcessiveTotalAttachedImageBytes_ReturnsValidationError()
    {
        GenerationModelMetadataDto metadata = CreateMetadataWithAttachmentLimits(
            maxCount: 3,
            maxSingleFileBytes: PngSignatureLength,
            maxTotalBytes: PngSignatureLength + 1L);
        HandlerTestContext context = new(metadata);
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto("reference-1.png", "image/png", CreatePngContent()),
                new AttachedImageDto("reference-2.png", "image/png", CreatePngContent())
            ]);

        Result<GenerationBatchDto> result = await context.Handler.Handle(command, CancellationToken.None);

        context.AssertValidationRejected(result);
    }

    [Fact]
    public async Task Handle_WithUnsupportedAttachmentContentType_ReturnsValidationError()
    {
        HandlerTestContext context = new();
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto("reference.gif", "image/gif", CreateGifContent())
            ]);

        Result<GenerationBatchDto> result = await context.Handler.Handle(command, CancellationToken.None);

        context.AssertValidationRejected(result);
    }

    [Fact]
    public async Task Handle_WithUnknownModel_ReturnsNotFound()
    {
        HandlerTestContext context = new();
        CreateImageGenerationCommand command = CreateCommand(modelId: "unknown");

        Result<GenerationBatchDto> result = await context.Handler.Handle(command, CancellationToken.None);

        context.AssertModelNotFound(result);
    }

    [Fact]
    public async Task Handle_WithValidRequest_DoesNotMutateServerState()
    {
        using TemporaryCurrentDirectory outputDirectory = new(
            typeof(CreateImageGenerationHandlerTests),
            nameof(Handle_WithValidRequest_DoesNotMutateServerState));
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand();

        GenerationBatchDto batch = await HandleSuccessfullyAsync(handler, command);

        GenerationItemDto item = batch.Items.Single();
        item.ImagePath.Should().BeNull();
        Directory.Exists(Path.Combine(outputDirectory.DirectoryPath, "generations")).Should().BeFalse();
        outputDirectory.GetEntries().Should().BeEmpty();
    }

    [Theory]
    [InlineData(
        ImageGenerationProviderFailureKind.RequestRejected,
        GenerationProviderFailureErrorCodes.RequestRejected)]
    [InlineData(
        ImageGenerationProviderFailureKind.Authentication,
        GenerationProviderFailureErrorCodes.Authentication)]
    [InlineData(
        ImageGenerationProviderFailureKind.Authorization,
        GenerationProviderFailureErrorCodes.Authorization)]
    [InlineData(
        ImageGenerationProviderFailureKind.ResourceNotFound,
        GenerationProviderFailureErrorCodes.ResourceNotFound)]
    [InlineData(
        ImageGenerationProviderFailureKind.RateLimited,
        GenerationProviderFailureErrorCodes.RateLimited)]
    [InlineData(
        ImageGenerationProviderFailureKind.InternalError,
        GenerationProviderFailureErrorCodes.InternalError)]
    [InlineData(
        ImageGenerationProviderFailureKind.InvalidResponse,
        GenerationProviderFailureErrorCodes.InvalidResponse)]
    [InlineData(
        ImageGenerationProviderFailureKind.Timeout,
        GenerationProviderFailureErrorCodes.Timeout)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unavailable,
        GenerationProviderFailureErrorCodes.Unavailable)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unknown,
        GenerationProviderFailureErrorCodes.Unknown)]
    public async Task Handle_WhenContentProviderReturnsProviderFailure_MapsErrorCode(
        ImageGenerationProviderFailureKind failureKind,
        string expectedErrorCode)
    {
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = new();
        ImageGenerationProviderException exception = new(
            failureKind,
            "Provider response is invalid.");
        contentProvider
            .Setup(provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        CreateImageGenerationHandler handler = CreateHandler(
            outputPlanner.Object,
            contentProvider.Object);
        CreateImageGenerationCommand command = CreateCommand();

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsUnavailable.Should().BeTrue();
        result.ErrorCode.Should().Be(expectedErrorCode);
        result.ErrorMessage.Should().Be("Провайдер генерации временно недоступен.");
    }

    private static CreateImageGenerationHandler CreateHandler(
        IImageGenerationOutputPlanner outputPlanner,
        IImageGenerationContentProvider contentProvider,
        ImageModelRegistry? registry = null)
    {
        ImageModelRegistry modelRegistry =
            registry ?? MetadataImageModelTestFactory.CreateRegistry();

        return new CreateImageGenerationHandler(
            modelRegistry,
            outputPlanner,
            contentProvider,
            new TestDateTimeProvider());
    }

    private static async Task<GenerationBatchDto> HandleSuccessfullyAsync(
        CreateImageGenerationHandler handler,
        CreateImageGenerationCommand command)
    {
        Result<GenerationBatchDto> result = await handler.Handle(
            command,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        return result.Value
            ?? throw new InvalidOperationException("Generation batch result is missing.");
    }

    private static Mock<IImageGenerationOutputPlanner> CreatePlannerMock(
        Action<Guid>? captureBatchId = null,
        Action<ImageGenerationRequestDto>? captureRequest = null)
    {
        Mock<IImageGenerationOutputPlanner> outputPlanner = new();
        ImageGenerationOutputPlan outputPlan = new(
        [
            new(
                    ItemId,
                    CreatedAtUtc)
        ]);

        outputPlanner
            .Setup(planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()))
            .Callback<ImageGenerationRequestDto, Guid, string>(
                (request, batchId, _) =>
                {
                    captureBatchId?.Invoke(batchId);
                    captureRequest?.Invoke(request);
                })
            .Returns(outputPlan);

        return outputPlanner;
    }

    private static Mock<IImageGenerationOutputPlanner> CreatePlannerMockForRequestCount()
    {
        Guid[] itemIds =
        [
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444")
        ];
        Mock<IImageGenerationOutputPlanner> outputPlanner = new();

        outputPlanner
            .Setup(planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()))
            .Returns<ImageGenerationRequestDto, Guid, string>((request, _, _) =>
            {
                IReadOnlyList<ImageGenerationOutputItemPlan> items = Enumerable
                    .Range(0, request.GenerationCount)
                    .Select(index => new ImageGenerationOutputItemPlan(
                        itemIds[index],
                        CreatedAtUtc))
                    .ToList();

                return new ImageGenerationOutputPlan(items);
            });

        return outputPlanner;
    }

    private static Mock<IImageGenerationContentProvider> CreateContentProviderMock(
        Action<ImageGenerationRequestDto>? captureRequest = null,
        Action<ImageGenerationContentProviderContext>? captureContext = null,
        ImageGenerationContentResult? content = null)
    {
        Mock<IImageGenerationContentProvider> contentProvider = new();
        ImageGenerationContentResult providerContent = content ?? new ImageGenerationContentResult("image/png", "iVBORw0KGgo=");

        contentProvider
            .Setup(provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<ImageGenerationContentProviderContext, CancellationToken>(
                (context, _) =>
                {
                    captureRequest?.Invoke(context.Request);
                    captureContext?.Invoke(context);
                })
            .ReturnsAsync(providerContent);

        return contentProvider;
    }

    private static CreateImageGenerationCommand CreateCommand(
        string? modelId = null,
        int generationCount = 1,
        IReadOnlyList<AttachedImageDto>? attachedImages = null,
        string? providerCredential = null)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        ImageGenerationRequestDto request = ImageGenerationRequestDtoTestFactory.Create(
            modelId: modelId,
            aspectRatio: metadata.AspectRatios.First(),
            resolution: metadata.Resolutions.First(),
            temperature: metadata.Temperature.Default,
            generationCount: generationCount,
            attachedImages: attachedImages);

        return new CreateImageGenerationCommand(request, providerCredential);
    }

    private static byte[] CreatePngContent()
    {
        return GenerationImageFileSignatures.Png.ToArray();
    }

    private static byte[] CreateLargePngContent(int length)
    {
        byte[] content = new byte[length];
        CreatePngContent().CopyTo(content, 0);

        return content;
    }

    private static byte[] CreateGifContent()
    {
        return GenerationImageFileSignatures.Gif89A.ToArray();
    }

    private static GenerationModelMetadataDto CreateMetadataWithAttachmentLimits(
        int maxCount,
        long maxSingleFileBytes,
        long maxTotalBytes)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();

        return metadata with
        {
            Attachments = new GenerationModelAttachmentMetadataDto(
                maxCount,
                maxSingleFileBytes,
                maxTotalBytes,
                metadata.Attachments.SupportedContentTypes)
        };
    }

    private sealed class HandlerTestContext
    {
        public CreateImageGenerationHandler Handler { get; }

        private readonly Mock<IImageGenerationOutputPlanner> _outputPlanner;
        private readonly Mock<IImageGenerationContentProvider> _contentProvider;

        public HandlerTestContext()
            : this(MetadataImageModelTestFactory.CreateRegistry())
        {
        }

        public HandlerTestContext(GenerationModelMetadataDto metadata)
            : this(MetadataImageModelTestFactory.CreateRegistry(metadata))
        {
        }

        private HandlerTestContext(ImageModelRegistry registry)
        {
            _outputPlanner = CreatePlannerMock();
            _contentProvider = CreateContentProviderMock();
            Handler = CreateHandler(
                _outputPlanner.Object,
                _contentProvider.Object,
                registry);
        }

        public void AssertValidationRejected(Result<GenerationBatchDto> result)
        {
            result.IsValidationError.Should().BeTrue();
            result.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);

            AssertExecutionNotStarted();
        }

        public void AssertModelNotFound(Result<GenerationBatchDto> result)
        {
            result.IsNotFound.Should().BeTrue();
            result.ErrorCode.Should().Be(GenerationErrorCodes.ModelNotFound);

            AssertExecutionNotStarted();
        }

        private void AssertExecutionNotStarted()
        {
            _outputPlanner.Verify(
                planner => planner.CreatePlan(
                    It.IsAny<ImageGenerationRequestDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>()),
                Times.Never);
            _contentProvider.Verify(
                provider => provider.GetContentAsync(
                    It.IsAny<ImageGenerationContentProviderContext>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => CreatedAtUtc.AddSeconds(30);
    }

    private sealed class ParallelTrackingContentProvider : IImageGenerationContentProvider
    {
        public int StartedCount => Volatile.Read(ref _startedCount);
        public Task AllRequestsStarted => _allRequestsStarted.Task;

        private readonly TaskCompletionSource _allRequestsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseRequests = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _expectedRequestCount;
        private int _startedCount;

        public ParallelTrackingContentProvider(int expectedRequestCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedRequestCount);

            _expectedRequestCount = expectedRequestCount;
        }

        public async Task<ImageGenerationContentResult> GetContentAsync(
            ImageGenerationContentProviderContext context,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(context);

            int startedCount = Interlocked.Increment(ref _startedCount);

            if (startedCount == _expectedRequestCount)
            {
                _allRequestsStarted.TrySetResult();
            }

            await _releaseRequests.Task.WaitAsync(ct).ConfigureAwait(false);

            return new ImageGenerationContentResult("image/png", "iVBORw0KGgo=");
        }

        public void ReleaseRequests()
        {
            _releaseRequests.TrySetResult();
        }
    }
}
