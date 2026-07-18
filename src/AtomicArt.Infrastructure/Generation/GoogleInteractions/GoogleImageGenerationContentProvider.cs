using System.Text.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleImageGenerationContentProvider : IProviderImageGenerationContentProvider
{
    private readonly GoogleInteractionsRequestBuilder _requestBuilder;
    private readonly IGoogleInteractionsClient _client;
    private readonly GoogleInteractionsResponseParser _responseParser;
    private readonly GenerationUsagePriceCalculator _priceCalculator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<GoogleImageGenerationContentProvider> _logger;

    public string Provider => GenerationProviderIds.Google;

    public GoogleImageGenerationContentProvider(
        GoogleInteractionsRequestBuilder requestBuilder,
        IGoogleInteractionsClient client,
        GoogleInteractionsResponseParser responseParser,
        GenerationUsagePriceCalculator priceCalculator,
        IDateTimeProvider dateTimeProvider,
        ILogger<GoogleImageGenerationContentProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(requestBuilder);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(priceCalculator);
        ArgumentNullException.ThrowIfNull(dateTimeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _requestBuilder = requestBuilder;
        _client = client;
        _responseParser = responseParser;
        _priceCalculator = priceCalculator;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<ImageGenerationContentResult> GetContentAsync(
        ImageGenerationContentProviderContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateContext(context);

        DateTime startedAtUtc = _dateTimeProvider.UtcNow;
        string requestJson = _requestBuilder.Build(context);
        string responseJson = await _client
            .CreateInteractionAsync(requestJson, context.ProviderCredential ?? string.Empty, ct)
            .ConfigureAwait(false);
        GoogleInteractionsResult result = ParseResponse(responseJson);
        DateTime completedAtUtc = _dateTimeProvider.UtcNow;
        GoogleInteractionImageContent image = result.Images[0];
        GenerationPriceDto? price = _priceCalculator.Calculate(
            context.Request.ModelId,
            context.Pricing,
            result.Usage,
            context.Request.Resolution,
            result.Images.Count);

        _logger.LogInformation(
            "Google image generation response processed in {ElapsedMilliseconds} ms. Images {ImageCount}; input tokens {InputTokens}; output tokens {OutputTokens}; total tokens {TotalTokens}.",
            Math.Max(0L, (long)(completedAtUtc - startedAtUtc).TotalMilliseconds),
            result.Images.Count,
            result.Usage?.TotalInputTokens,
            result.Usage?.TotalOutputTokens,
            result.Usage?.TotalTokens);

        return new ImageGenerationContentResult(
            image.ContentType,
            image.Base64Data,
            result.Usage,
            price,
            completedAtUtc,
            completedAtUtc - startedAtUtc);
    }

    private GoogleInteractionsResult ParseResponse(string responseJson)
    {
        try
        {
            return _responseParser.Parse(responseJson);
        }
        catch (GoogleInteractionsException exception) when (exception.NoImageDiagnostics is not null)
        {
            LogNoImageResponse(exception, exception.NoImageDiagnostics);

            throw;
        }
        catch (GoogleInteractionsException exception)
        {
            _logger.LogWarning(
                exception,
                "Google image generation response was rejected with provider failure {FailureKind}. Response size {ResponseSize} characters.",
                exception.FailureKind,
                responseJson.Length);

            throw;
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Google image generation provider returned malformed JSON. Response size {ResponseSize} characters.",
                responseJson.Length);

            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider returned a malformed response.");
        }
    }

    private void LogNoImageResponse(
        GoogleInteractionsException exception,
        GoogleInteractionsNoImageDiagnostics diagnostics)
    {
        _logger.LogWarning(
            exception,
            "Google image generation provider returned no image. Category {Category}; Status {Status}; HasOutputImage {HasOutputImage}; HasOutput {HasOutput}; HasOutputImages {HasOutputImages}; HasStepsTextContent {HasStepsTextContent}; HasModelOutputTextContent {HasModelOutputTextContent}; HasContentTextContent {HasContentTextContent}; TextContentLength {TextContentLength}; TextContentItemCount {TextContentItemCount}",
            diagnostics.Category,
            diagnostics.Status,
            diagnostics.HasOutputImage,
            diagnostics.HasOutput,
            diagnostics.HasOutputImages,
            diagnostics.HasStepsTextContent,
            diagnostics.HasModelOutputTextContent,
            diagnostics.HasContentTextContent,
            diagnostics.TextContentLength,
            diagnostics.TextContentItemCount);
    }

    private void ValidateContext(ImageGenerationContentProviderContext context)
    {
        if (!string.Equals(context.Provider, GenerationProviderIds.Google, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Google image generation rejected a context routed to an unsupported provider.");

            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The model provider is not supported.");
        }

        GenerationProviderCredentialRequirement credentialRequirement =
            GenerationProviderCredentialRequirements.Resolve(context.Provider);

        if (credentialRequirement.RequiredForApplicationValidation
            && string.IsNullOrWhiteSpace(context.ProviderCredential))
        {
            _logger.LogWarning(
                "Google image generation rejected a context without provider credentials.");

            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.Authentication,
                "The temporary provider credential was not supplied.");
        }
    }
}
