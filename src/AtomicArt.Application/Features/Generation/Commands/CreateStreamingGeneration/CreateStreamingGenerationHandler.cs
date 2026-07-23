using Microsoft.Extensions.Logging;

using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Common.Models;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using DomainGenerationErrorCodes = AtomicArt.Domain.Generation.GenerationErrorCodes;
using MediatR;

namespace AtomicArt.Application.Features.Generation.Commands.CreateStreamingGeneration;

public sealed class CreateStreamingGenerationHandler
    : IRequestHandler<CreateStreamingGenerationCommand, GenerationAttemptPreparation>
{
    private readonly IImageModelRegistry _modelRegistry;
    private readonly IStreamingImageGenerationProvider _provider;
    private readonly StreamingGenerationRequestValidator _requestValidator;
    private readonly GenerationUsagePriceCalculator _priceCalculator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateStreamingGenerationHandler> _logger;

    public CreateStreamingGenerationHandler(
        IImageModelRegistry modelRegistry,
        IStreamingImageGenerationProvider provider,
        StreamingGenerationRequestValidator requestValidator,
        GenerationUsagePriceCalculator priceCalculator,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateStreamingGenerationHandler> logger)
    {
        _modelRegistry = modelRegistry
            ?? throw new ArgumentNullException(nameof(modelRegistry));
        _provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
        _requestValidator = requestValidator
            ?? throw new ArgumentNullException(nameof(requestValidator));
        _priceCalculator = priceCalculator
            ?? throw new ArgumentNullException(nameof(priceCalculator));
        _dateTimeProvider = dateTimeProvider
            ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GenerationAttemptPreparation> Handle(
        CreateStreamingGenerationCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        IImageModelDefinition? modelDefinition =
            _modelRegistry.GetById(command.Metadata.ModelId);

        if (modelDefinition is null)
        {
            return GenerationAttemptPreparation.NotFound(
                DomainGenerationErrorCodes.ModelNotFound);
        }

        Result<StreamingImageGenerationRequest> validationResult =
            await _requestValidator
                .ValidateAsync(
                    command.Metadata,
                    command.Attachments,
                    modelDefinition,
                    ct)
                .ConfigureAwait(false);

        if (validationResult is not { IsSuccess: true, Value: { } request })
        {
            return GenerationAttemptPreparation.Validation(
                validationResult.ErrorCode
                    ?? DomainGenerationErrorCodes.ModelRequestValidation);
        }

        GenerationProviderCredentialRequirement credentialRequirement =
            GenerationProviderCredentialRequirements.Resolve(
                modelDefinition.Metadata.Provider);

        if (credentialRequirement.RequiredForApplicationValidation
            && string.IsNullOrWhiteSpace(command.ProviderCredential))
        {
            return GenerationAttemptPreparation.ProviderFailure(
                ImageGenerationProviderFailureKind.Authentication,
                GenerationProviderFailureErrorCodes.Authentication,
                false);
        }

        DateTime startedAtUtc = _dateTimeProvider.UtcNow;
        StreamingGenerationProviderContext context = new(
            request,
            modelDefinition.Metadata.Provider,
            modelDefinition.Metadata.ProviderModelId,
            modelDefinition.Metadata.Pricing,
            command.ProviderCredential,
            modelDefinition.Metadata.TransportLimits);

        try
        {
            IProviderGenerationStream providerStream = await _provider
                .CreateStreamAsync(context, ct)
                .ConfigureAwait(false);
            Guid batchId = Guid.NewGuid();
            Guid itemId = Guid.NewGuid();
            StreamingGenerationAttempt attempt = new(
                providerStream,
                _priceCalculator,
                _dateTimeProvider,
                request,
                modelDefinition.Metadata,
                modelDefinition.Metadata.Provider,
                batchId,
                itemId,
                startedAtUtc);

            _logger.LogInformation(
                "Generation attempt {AttemptNumber} for logical generation {LogicalGenerationId} started with provider {Provider}.",
                request.AttemptNumber,
                request.LogicalGenerationId,
                modelDefinition.Metadata.Provider);

            return GenerationAttemptPreparation.Success(attempt);
        }
        catch (ImageGenerationProviderException exception)
        {
            _logger.LogWarning(
                exception,
                "Provider rejected generation attempt {AttemptNumber} for logical generation {LogicalGenerationId} with failure {FailureKind}.",
                request.AttemptNumber,
                request.LogicalGenerationId,
                exception.FailureKind);
            string safeErrorCode =
                ImageGenerationProviderFailureCatalog.GetErrorCode(exception.FailureKind);

            return GenerationAttemptPreparation.ProviderFailure(
                exception.FailureKind,
                safeErrorCode,
                exception.Retryable);
        }
    }
}
