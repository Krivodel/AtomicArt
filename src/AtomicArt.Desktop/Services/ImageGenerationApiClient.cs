using System.Diagnostics;
using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class ImageGenerationApiClient : IImageGenerationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IApiEndpointService _apiEndpointService;
    private readonly ILogger<ImageGenerationApiClient> _logger;

    public ImageGenerationApiClient(
        HttpClient httpClient,
        IApiEndpointService apiEndpointService,
        ILogger<ImageGenerationApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(apiEndpointService);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _apiEndpointService = apiEndpointService;
        _logger = logger;
    }

    public async Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        string providerCredential,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Sending image generation request with {GenerationCount} requested results and {AttachmentCount} attachments.",
            request.GenerationCount,
            request.AttachedImages.Count);

        Uri requestUri = _apiEndpointService.CreateRequestUri(GenerationApiRoutes.Generations);

        if (!string.IsNullOrWhiteSpace(providerCredential))
        {
            EnsureTrustedProviderCredentialTarget(requestUri);
        }

        using HttpRequestMessage requestMessage = new(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrWhiteSpace(providerCredential))
        {
            requestMessage.Headers.TryAddWithoutValidation(
                GenerationApiRoutes.ProviderApiKeyHeaderName,
                providerCredential);
        }

        using HttpResponseMessage response = await _httpClient
            .SendAsync(requestMessage, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            SafeApiProblemDetailsReadResult problemDetails = await SafeApiProblemDetailsReader
                .TryReadErrorCodeAsync(response.Content, ct)
                .ConfigureAwait(false);
            SafeApiProblemDetailsReader.LogReadFailure(
                _logger,
                problemDetails,
                SafeApiProblemDetailsApi.ImageGeneration);
            _logger.LogWarning(
                "Image generation API returned HTTP {StatusCode} with error code {ErrorCode} after {ElapsedMilliseconds} ms.",
                (int)response.StatusCode,
                problemDetails.ErrorCode ?? "unavailable",
                stopwatch.ElapsedMilliseconds);
        }

        response.EnsureSuccessStatusCode();

        GenerationBatchDto? batch = await response.Content
            .ReadFromJsonAsync<GenerationBatchDto>(ct)
            .ConfigureAwait(false);

        if (batch is null)
        {
            throw new InvalidOperationException("Generation API returned an empty response.");
        }

        _logger.LogInformation(
            "Image generation API completed batch {BatchId} with {ItemCount} items after {ElapsedMilliseconds} ms.",
            batch.BatchId,
            batch.Items.Count,
            stopwatch.ElapsedMilliseconds);

        return batch;
    }

    private static void EnsureTrustedProviderCredentialTarget(Uri targetUri)
    {
        if (IsTrustedProviderCredentialTarget(targetUri))
        {
            return;
        }

        throw new InvalidOperationException(
            "Provider credential can be sent only to HTTP or HTTPS API endpoint.");
    }

    private static bool IsTrustedProviderCredentialTarget(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeHttp;
    }
}
