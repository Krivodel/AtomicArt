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
        ImageModelRegistry registry = CreateRegistry();
        Guid plannedBatchId = Guid.Empty;
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock(captureBatchId: batchId => plannedBatchId = batchId);
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand();

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        GenerationBatchDto batch = result.Value ?? throw new InvalidOperationException("Generation batch result is missing.");
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
                "Nano Banana 2"),
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
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ]);
        ImageGenerationContentResult content = new(
            "image/png",
            "iVBORw0KGgo=",
            usage,
            new GenerationPriceDto(0.0678m, "USD", "ActualProviderUsage"),
            completedAtUtc,
            generationDuration);
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock(content: content);
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand();

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        GenerationBatchDto batch = result.Value ?? throw new InvalidOperationException("Generation batch result is missing.");
        GenerationItemDto item = batch.Items.Single();
        item.CompletedAtUtc.Should().Be(completedAtUtc);
        item.GenerationDuration.Should().Be(generationDuration);
        item.Usage.Should().BeSameAs(usage);
        item.Price.Should().BeEquivalentTo(new GenerationPriceDto(0.0678m, "USD", "ActualProviderUsage"));
    }

    [Fact]
    public async Task Handle_WithProviderCredential_PassesProviderContextToContentProvider()
    {
        ImageGenerationContentProviderContext? capturedContext = null;
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock(
            captureContext: context => capturedContext = context);
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(providerCredential: "test-provider-key");

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        ImageGenerationContentProviderContext context = capturedContext
            ?? throw new InvalidOperationException("Provider context is missing.");
        context.ProviderCredential.Should().Be("test-provider-key");
        context.Provider.Should().Be(GenerationProviderIds.Google);
        context.ProviderModelId.Should().Be(ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().ProviderModelId);
        context.ItemIndex.Should().Be(0);
        context.Request.ModelId.Should().Be(command.Request.ModelId);
    }

    [Fact]
    public async Task Handle_WithNormalizableAttachment_PassesValidatedRequestToPlannerAndWriter()
    {
        ImageModelRegistry registry = CreateRegistry();
        ImageGenerationRequestDto? plannedRequest = null;
        ImageGenerationRequestDto? contentRequest = null;
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock(
            captureRequest: request => plannedRequest = request);
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock(
            captureRequest: request => contentRequest = request);
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new(
                    " source.png ",
                    " IMAGE/PNG ",
                    CreatePngContent())
            ]);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
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
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMockForRequestCount();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(generationCount: 2);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        GenerationBatchDto batch = result.Value ?? throw new InvalidOperationException("Generation batch result is missing.");
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
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMockForRequestCount();
        ParallelTrackingContentProvider contentProvider = new(expectedRequestCount: 2);
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider,
            new TestDateTimeProvider());
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
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(generationCount: 5);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be("ERR-GEN-004");
        outputPlanner.Verify(
            planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithTooManyAttachedImages_ReturnsValidationError()
    {
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        IReadOnlyList<AttachedImageDto> attachedImages = Enumerable
            .Range(0, 15)
            .Select(index => new AttachedImageDto(
                $"reference-{index}.png",
                "image/png",
                CreatePngContent()))
            .ToList();
        CreateImageGenerationCommand command = CreateCommand(attachedImages: attachedImages);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be("ERR-GEN-004");
        outputPlanner.Verify(
            planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithOversizedAttachedImage_ReturnsValidationError()
    {
        GenerationModelMetadataDto metadata = CreateMetadataWithAttachmentLimits(
            maxCount: 3,
            maxSingleFileBytes: PngSignatureLength,
            maxTotalBytes: PngSignatureLength * 3L);
        ImageModelRegistry registry = CreateRegistry(metadata);
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto("reference.png", "image/png", CreateLargePngContent(PngSignatureLength + 1))
            ]);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be("ERR-GEN-004");
        outputPlanner.Verify(
            planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithExcessiveTotalAttachedImageBytes_ReturnsValidationError()
    {
        GenerationModelMetadataDto metadata = CreateMetadataWithAttachmentLimits(
            maxCount: 3,
            maxSingleFileBytes: PngSignatureLength,
            maxTotalBytes: PngSignatureLength + 1L);
        ImageModelRegistry registry = CreateRegistry(metadata);
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto("reference-1.png", "image/png", CreatePngContent()),
                new AttachedImageDto("reference-2.png", "image/png", CreatePngContent())
            ]);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be("ERR-GEN-004");
        outputPlanner.Verify(
            planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnsupportedAttachmentContentType_ReturnsValidationError()
    {
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(
            attachedImages:
            [
                new AttachedImageDto("reference.gif", "image/gif", CreateGifContent())
            ]);

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsValidationError.Should().BeTrue();
        result.ErrorCode.Should().Be("ERR-GEN-004");
        outputPlanner.Verify(
            planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnknownModel_ReturnsNotFound()
    {
        ImageModelRegistry registry = CreateRegistry();
        Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
        Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand(modelId: "unknown");

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be("ERR-GEN-001");
        outputPlanner.Verify(
            planner => planner.CreatePlan(
                It.IsAny<ImageGenerationRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
        contentProvider.Verify(
            provider => provider.GetContentAsync(
                It.IsAny<ImageGenerationContentProviderContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidRequest_DoesNotMutateServerState()
    {
        string outputRoot = CreateCleanDirectory(nameof(Handle_WithValidRequest_DoesNotMutateServerState));
        string currentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(outputRoot);
            ImageModelRegistry registry = CreateRegistry();
            Mock<IImageGenerationOutputPlanner> outputPlanner = CreatePlannerMock();
            Mock<IImageGenerationContentProvider> contentProvider = CreateContentProviderMock();
            CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
            CreateImageGenerationCommand command = CreateCommand();

            Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            GenerationBatchDto batch = result.Value ?? throw new InvalidOperationException("Generation batch result is missing.");
            GenerationItemDto item = batch.Items.Single();
            item.ImagePath.Should().BeNull();
            Directory.Exists(Path.Combine(outputRoot, "generations")).Should().BeFalse();
            Directory
                .EnumerateFileSystemEntries(outputRoot, "*", SearchOption.AllDirectories)
                .Should()
                .BeEmpty();
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
            DeleteDirectoryIfExists(outputRoot);
        }
    }

    [Theory]
    [InlineData(
        ImageGenerationProviderFailureKind.RequestRejected,
        ImageGenerationProviderErrorCodes.RequestRejected)]
    [InlineData(
        ImageGenerationProviderFailureKind.Authentication,
        ImageGenerationProviderErrorCodes.Authentication)]
    [InlineData(
        ImageGenerationProviderFailureKind.Authorization,
        ImageGenerationProviderErrorCodes.Authorization)]
    [InlineData(
        ImageGenerationProviderFailureKind.ResourceNotFound,
        ImageGenerationProviderErrorCodes.ResourceNotFound)]
    [InlineData(
        ImageGenerationProviderFailureKind.RateLimited,
        ImageGenerationProviderErrorCodes.RateLimited)]
    [InlineData(
        ImageGenerationProviderFailureKind.InternalError,
        ImageGenerationProviderErrorCodes.InternalError)]
    [InlineData(
        ImageGenerationProviderFailureKind.InvalidResponse,
        ImageGenerationProviderErrorCodes.InvalidResponse)]
    [InlineData(
        ImageGenerationProviderFailureKind.Timeout,
        ImageGenerationProviderErrorCodes.Timeout)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unavailable,
        ImageGenerationProviderErrorCodes.Unavailable)]
    [InlineData(
        ImageGenerationProviderFailureKind.Unknown,
        ImageGenerationProviderErrorCodes.Unknown)]
    public async Task Handle_WhenContentProviderReturnsProviderFailure_MapsErrorCode(
        ImageGenerationProviderFailureKind failureKind,
        string expectedErrorCode)
    {
        ImageModelRegistry registry = CreateRegistry();
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
        CreateImageGenerationHandler handler = new(
            registry,
            outputPlanner.Object,
            contentProvider.Object,
            new TestDateTimeProvider());
        CreateImageGenerationCommand command = CreateCommand();

        Result<GenerationBatchDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsUnavailable.Should().BeTrue();
        result.ErrorCode.Should().Be(expectedErrorCode);
        result.ErrorMessage.Should().Be("Провайдер генерации временно недоступен.");
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
        ImageGenerationRequestDto request = new(
            modelId ?? ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            "Prompt",
            metadata.AspectRatios.First(),
            metadata.Resolutions.First(),
            metadata.Temperature.Default,
            generationCount,
            attachedImages ?? []);

        return new CreateImageGenerationCommand(request, providerCredential);
    }

    private static byte[] CreatePngContent()
    {
        byte[] content =
        [
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A
        ];

        return content;
    }

    private static byte[] CreateLargePngContent(int length)
    {
        byte[] content = new byte[length];
        CreatePngContent().CopyTo(content, 0);

        return content;
    }

    private static byte[] CreateGifContent()
    {
        byte[] content = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];

        return content;
    }

    private static ImageModelRegistry CreateRegistry(GenerationModelMetadataDto? metadata = null)
    {
        GenerationModelCatalogDto catalog = metadata is null
            ? ApiModelMetadataTestCatalog.LoadCatalog()
            : new GenerationModelCatalogDto([metadata]);

        return new ImageModelRegistry(
            catalog,
            CreateFactories());
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

    private static IReadOnlyList<IAttachedImageFormat> CreateFormats()
    {
        return GenerationImageFileFormats.All
            .Select<GenerationImageFileFormatDescriptor, IAttachedImageFormat>(format => new AttachedImageFormat(format))
            .ToList();
    }

    private static IReadOnlyList<IImageModelDefinitionFactory> CreateFactories()
    {
        return
        [
            new MetadataImageModelDefinitionFactory(
                new GenerationModelRules([new MetadataGenerationModelRules()]),
                CreateFormats())
        ];
    }

    private static string CreateCleanDirectory(string testName)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "AtomicArt.Application.Tests",
            testName);

        DeleteDirectoryIfExists(directoryPath);
        Directory.CreateDirectory(directoryPath);

        return directoryPath;
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, true);
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
