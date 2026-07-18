using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;
using MediatR;

namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

public sealed class CreateImageGenerationHandler : IRequestHandler<CreateImageGenerationCommand, Result<GenerationBatchDto>>
{
    private readonly IImageModelRegistry _modelRegistry;
    private readonly IImageGenerationOutputPlanner _outputPlanner;
    private readonly IImageGenerationContentProvider _contentProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateImageGenerationHandler> _logger;

    public CreateImageGenerationHandler(
        IImageModelRegistry modelRegistry,
        IImageGenerationOutputPlanner outputPlanner,
        IImageGenerationContentProvider contentProvider,
        IDateTimeProvider dateTimeProvider)
        : this(
            modelRegistry,
            outputPlanner,
            contentProvider,
            dateTimeProvider,
            NullLogger<CreateImageGenerationHandler>.Instance)
    {
    }

    public CreateImageGenerationHandler(
        IImageModelRegistry modelRegistry,
        IImageGenerationOutputPlanner outputPlanner,
        IImageGenerationContentProvider contentProvider,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateImageGenerationHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(modelRegistry);
        ArgumentNullException.ThrowIfNull(outputPlanner);
        ArgumentNullException.ThrowIfNull(contentProvider);
        ArgumentNullException.ThrowIfNull(dateTimeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _modelRegistry = modelRegistry;
        _outputPlanner = outputPlanner;
        _contentProvider = contentProvider;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<GenerationBatchDto>> Handle(
        CreateImageGenerationCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        ImageGenerationRequestDto request = command.Request;
        int attachedImageCount = request.AttachedImages?.Count ?? 0;
        long totalAttachedImageBytes = request.AttachedImages?
            .Where(image => image is not null)
            .Sum(image => (long)image.Content.Length)
            ?? 0L;

        _logger.LogInformation(
            "Generation request was accepted for processing. Requested results: {GenerationCount}; attachments: {AttachedImageCount}; attachment bytes: {AttachedImageBytes}; provider credentials present: {HasProviderCredential}.",
            request.GenerationCount,
            attachedImageCount,
            totalAttachedImageBytes,
            !string.IsNullOrWhiteSpace(command.ProviderCredential));

        IImageModelDefinition? modelDefinition = _modelRegistry.GetById(request.ModelId);

        if (modelDefinition is null)
        {
            _logger.LogWarning("Generation request was rejected because the model was not found.");

            return Result<GenerationBatchDto>.NotFound(
                GenerationErrorCodes.ModelNotFound,
                "Модель генерации не найдена.");
        }

        Result<ImageGenerationRequestDto> normalizationResult = modelDefinition.Validate(request);

        if (!normalizationResult.IsSuccess)
        {
            _logger.LogWarning(
                "Generation request was rejected during normalization with error code {ErrorCode}.",
                normalizationResult.ErrorCode);

            return CreateGenerationFailure(normalizationResult);
        }

        ImageGenerationRequestDto validatedRequest = normalizationResult.Value
            ?? throw new InvalidOperationException("Validated generation request result is missing.");
        Guid batchId = Guid.NewGuid();
        ImageGenerationOutputPlan outputPlan = _outputPlanner.CreatePlan(
            validatedRequest,
            batchId,
            modelDefinition.DisplayName);

        _logger.LogInformation(
            "Generation batch {BatchId} was planned for provider {Provider} with {ItemCount} items.",
            batchId,
            modelDefinition.Provider,
            outputPlan.Items.Count);

        Result<GenerationBatchDto> batchResult = await CreateBatchAsync(
                batchId,
                command.ProviderCredential,
                validatedRequest,
                modelDefinition,
                outputPlan,
                ct)
            .ConfigureAwait(false);

        return batchResult;
    }

    private static Result<GenerationBatchDto> CreateGenerationFailure(Result<ImageGenerationRequestDto> result)
    {
        if (result.IsNotFound)
        {
            return Result<GenerationBatchDto>.NotFound(
                result.ErrorCode ?? GenerationErrorCodes.ModelNotFound,
                result.ErrorMessage ?? "Модель генерации не найдена.");
        }

        if (result.IsUnavailable)
        {
            return Result<GenerationBatchDto>.Unavailable(
                result.ErrorCode ?? GenerationErrorCodes.ModelRequestValidation,
                result.ErrorMessage ?? "Запрос генерации временно недоступен.");
        }

        return Result<GenerationBatchDto>.ValidationError(
            result.ErrorCode ?? GenerationErrorCodes.ModelRequestValidation,
            result.ErrorMessage ?? "Запрос генерации не прошёл проверку.");
    }

    private async Task<Result<GenerationBatchDto>> CreateBatchAsync(
        Guid batchId,
        string? providerCredential,
        ImageGenerationRequestDto request,
        IImageModelDefinition modelDefinition,
        ImageGenerationOutputPlan outputPlan,
        CancellationToken ct)
    {
        Task<GenerationItemDto>[] itemTasks = outputPlan.Items
            .Select((itemPlan, itemIndex) => CreateItemAsync(
                providerCredential,
                request,
                modelDefinition,
                itemPlan,
                itemIndex,
                ct))
            .ToArray();

        try
        {
            GenerationItemDto[] items = await Task
                .WhenAll(itemTasks)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Generation batch {BatchId} completed with {ItemCount} items.",
                batchId,
                items.Length);

            return Result<GenerationBatchDto>.Success(new GenerationBatchDto(batchId, items));
        }
        catch (ImageGenerationProviderException exception)
        {
            _logger.LogWarning(
                exception,
                "Generation batch {BatchId} failed with provider failure category {FailureKind}.",
                batchId,
                exception.FailureKind);

            return CreateProviderFailure(exception);
        }
    }

    private static Result<GenerationBatchDto> CreateProviderFailure(
        ImageGenerationProviderException exception)
    {
        string errorCode = exception.FailureKind switch
        {
            ImageGenerationProviderFailureKind.RequestRejected => ImageGenerationProviderErrorCodes.RequestRejected,
            ImageGenerationProviderFailureKind.Authentication => ImageGenerationProviderErrorCodes.Authentication,
            ImageGenerationProviderFailureKind.Authorization => ImageGenerationProviderErrorCodes.Authorization,
            ImageGenerationProviderFailureKind.ResourceNotFound => ImageGenerationProviderErrorCodes.ResourceNotFound,
            ImageGenerationProviderFailureKind.RateLimited => ImageGenerationProviderErrorCodes.RateLimited,
            ImageGenerationProviderFailureKind.InternalError => ImageGenerationProviderErrorCodes.InternalError,
            ImageGenerationProviderFailureKind.Timeout => ImageGenerationProviderErrorCodes.Timeout,
            ImageGenerationProviderFailureKind.InvalidResponse => ImageGenerationProviderErrorCodes.InvalidResponse,
            ImageGenerationProviderFailureKind.Unavailable => ImageGenerationProviderErrorCodes.Unavailable,
            _ => ImageGenerationProviderErrorCodes.Unknown
        };

        return Result<GenerationBatchDto>.Unavailable(
            errorCode,
            "Провайдер генерации временно недоступен.");
    }

    private GenerationItemDto CreateItem(
        ImageGenerationRequestDto request,
        IImageModelDefinition modelDefinition,
        ImageGenerationOutputItemPlan itemPlan,
        ImageGenerationContentResult content,
        DateTime startedAtUtc,
        DateTime completedAtUtc)
    {
        GenerationImageContentDto imageContent = new(content.ContentType, content.Base64Data);
        TimeSpan generationDuration = content.GenerationDuration ?? completedAtUtc - startedAtUtc;

        if (generationDuration < TimeSpan.Zero)
        {
            generationDuration = TimeSpan.Zero;
        }

        return new GenerationItemDto(
            itemPlan.Id,
            request.ModelId,
            modelDefinition.DisplayName,
            request.Prompt,
            request.AspectRatio,
            request.Resolution,
            itemPlan.CreatedAtUtc,
            GenerationItemStatus.Generated,
            null,
            imageContent,
            completedAtUtc,
            generationDuration,
            content.Price,
            content.Usage);
    }

    private async Task<GenerationItemDto> CreateItemAsync(
        string? providerCredential,
        ImageGenerationRequestDto request,
        IImageModelDefinition modelDefinition,
        ImageGenerationOutputItemPlan itemPlan,
        int itemIndex,
        CancellationToken ct)
    {
        DateTime startedAtUtc = _dateTimeProvider.UtcNow;

        _logger.LogDebug(
            "Generation item {ItemId} at index {ItemIndex} started for provider {Provider}.",
            itemPlan.Id,
            itemIndex,
            modelDefinition.Provider);

        ImageGenerationContentProviderContext context = new(
            request,
            modelDefinition.Provider,
            modelDefinition.ProviderModelId,
            modelDefinition.Pricing,
            itemIndex,
            providerCredential);
        ImageGenerationContentResult content = await _contentProvider
            .GetContentAsync(context, ct)
            .ConfigureAwait(false);
        DateTime completedAtUtc = content.CompletedAtUtc ?? _dateTimeProvider.UtcNow;
        TimeSpan generationDuration = content.GenerationDuration ?? completedAtUtc - startedAtUtc;

        _logger.LogInformation(
            "Generation item {ItemId} at index {ItemIndex} was created by provider {Provider} in {ElapsedMilliseconds} ms. Content type: {ContentType}; Base64 length: {Base64Length} characters.",
            itemPlan.Id,
            itemIndex,
            modelDefinition.Provider,
            Math.Max(0L, (long)generationDuration.TotalMilliseconds),
            content.ContentType,
            content.Base64Data.Length);

        return CreateItem(request, modelDefinition, itemPlan, content, startedAtUtc, completedAtUtc);
    }
}
