using Microsoft.Extensions.Logging;

using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Features.Generation.Models;

public sealed class StreamingGenerationAttempt : IAsyncDisposable
{
    public string ProviderResponseContentType => _providerStream.ContentType;
    public string ProviderId => _providerId;

    private readonly IProviderGenerationStream _providerStream;
    private readonly GenerationUsagePriceCalculator _priceCalculator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<StreamingGenerationAttempt> _logger;
    private readonly StreamingImageGenerationRequest _request;
    private readonly GenerationModelMetadataDto _model;
    private readonly string _providerId;
    private readonly Guid _batchId;
    private readonly Guid _itemId;
    private readonly DateTime _startedAtUtc;
    private readonly long _emergencyMaxProviderResponseBytes;

    public StreamingGenerationAttempt(
        IProviderGenerationStream providerStream,
        GenerationUsagePriceCalculator priceCalculator,
        IDateTimeProvider dateTimeProvider,
        ILogger<StreamingGenerationAttempt> logger,
        StreamingImageGenerationRequest request,
        GenerationModelMetadataDto model,
        string providerId,
        Guid batchId,
        Guid itemId,
        DateTime startedAtUtc,
        long emergencyMaxProviderResponseBytes)
    {
        ArgumentNullException.ThrowIfNull(providerStream);
        ArgumentNullException.ThrowIfNull(priceCalculator);
        ArgumentNullException.ThrowIfNull(dateTimeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            emergencyMaxProviderResponseBytes,
            1L);

        _providerStream = providerStream;
        _priceCalculator = priceCalculator;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _request = request;
        _model = model;
        _providerId = providerId;
        _batchId = batchId;
        _itemId = itemId;
        _startedAtUtc = startedAtUtc;
        _emergencyMaxProviderResponseBytes =
            emergencyMaxProviderResponseBytes;
    }

    public async Task<GenerationAttemptMetadataDto> CopyProviderResponseAsync(
        Stream destination,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            await _providerStream
                .CopyToAsync(
                    destination,
                    GetMaximumProviderResponseBytes(),
                    ct)
                .ConfigureAwait(false);
            ProviderGenerationSummary summary = _providerStream.Summary
                ?? throw new InvalidOperationException(
                    "Provider stream completed without a summary.");
            ValidateSummary(summary);
            DateTime completedAtUtc = _dateTimeProvider.UtcNow;
            TimeSpan duration = NormalizeDuration(completedAtUtc - _startedAtUtc);
            GenerationPriceDto? price = _priceCalculator.Calculate(
                _request.ModelId,
                _model.Pricing,
                summary.Usage,
                _request.Resolution,
                summary.ResultCount);

            return new GenerationAttemptMetadataDto(
                _request.LogicalGenerationId,
                _request.AttemptNumber,
                _providerId,
                _batchId,
                _itemId,
                _model.DisplayName,
                GenerationItemStatus.Generated,
                summary.State,
                summary.ResultCount,
                summary.ContentTypes,
                summary.Usage,
                price,
                completedAtUtc,
                duration,
                null,
                false);
        }
        catch (ImageGenerationProviderException exception)
        {
            DateTime completedAtUtc = _dateTimeProvider.UtcNow;
            string errorCode = ImageGenerationProviderFailureCatalog.GetErrorCode(
                exception.FailureKind);

            _logger.LogWarning(
                exception,
                "Streaming generation attempt {AttemptNumber} for logical generation {LogicalGenerationId} failed after provider response streaming with provider {ProviderId} and failure {FailureKind}. Safe error code {SafeErrorCode}; retryable {Retryable}.",
                _request.AttemptNumber,
                _request.LogicalGenerationId,
                _providerId,
                exception.FailureKind,
                errorCode,
                exception.Retryable);

            return new GenerationAttemptMetadataDto(
                _request.LogicalGenerationId,
                _request.AttemptNumber,
                _providerId,
                _batchId,
                _itemId,
                _model.DisplayName,
                GenerationItemStatus.Failed,
                null,
                0,
                new List<string>(),
                null,
                null,
                completedAtUtc,
                NormalizeDuration(completedAtUtc - _startedAtUtc),
                errorCode,
                exception.Retryable);
        }
    }

    public ValueTask DisposeAsync()
    {
        return _providerStream.DisposeAsync();
    }

    private static TimeSpan NormalizeDuration(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    private long GetMaximumProviderResponseBytes()
    {
        long modelLimit = _model.TransportLimits?.MaxResponseBytes
            ?? _emergencyMaxProviderResponseBytes;

        return Math.Min(
            modelLimit,
            _emergencyMaxProviderResponseBytes);
    }

    private void ValidateSummary(ProviderGenerationSummary summary)
    {
        GenerationModelTransportLimitsDto? limits = _model.TransportLimits;

        if (limits is null)
        {
            return;
        }

        bool hasInvalidContentType = summary.ContentTypes.Any(
            contentType => !limits.AllowedResponseContentTypes.Contains(
                contentType,
                StringComparer.OrdinalIgnoreCase));

        if (summary.ResultCount <= 0
            || summary.ResultCount > limits.MaxResultCount
            || summary.ContentTypes.Count != summary.ResultCount
            || hasInvalidContentType)
        {
            throw new ImageGenerationProviderException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider response violates model limits.");
        }
    }
}
