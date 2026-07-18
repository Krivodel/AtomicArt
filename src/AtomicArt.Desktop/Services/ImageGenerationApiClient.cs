using System.Diagnostics;
using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public sealed class ImageGenerationApiClient
    : AtomicArtApiClient, IImageGenerationApiClient
{
    public ImageGenerationApiClient(
        HttpClient httpClient,
        IApiEndpointService apiEndpointService,
        ILogger<ImageGenerationApiClient> logger)
        : base(httpClient, apiEndpointService, logger)
    {
    }

    public async Task<GenerationBatchDto> CreateGenerationAsync(
        ImageGenerationRequestDto request,
        string providerCredential,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Stopwatch stopwatch = Stopwatch.StartNew();

        Logger.LogInformation(
            "Sending image generation request with {GenerationCount} requested results and {AttachmentCount} attachments.",
            request.GenerationCount,
            request.AttachedImages.Count);

        Uri requestUri = ApiEndpointService.CreateRequestUri(GenerationApiRoutes.Generations);

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

        using HttpResponseMessage response = await HttpClient
            .SendAsync(requestMessage, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await SafeApiProblemDetailsReader
                .LogResponseFailureAsync(
                    Logger,
                    response,
                    SafeApiProblemDetailsApi.ImageGeneration,
                    errorCode => Logger.LogWarning(
                        "Image generation API returned HTTP {StatusCode} with error code {ErrorCode} after {ElapsedMilliseconds} ms.",
                        (int)response.StatusCode,
                        errorCode,
                        stopwatch.ElapsedMilliseconds),
                    ct)
                .ConfigureAwait(false);
        }

        response.EnsureSuccessStatusCode();

        GenerationBatchDto? batch = await response.Content
            .ReadFromJsonAsync<GenerationBatchDto>(ct)
            .ConfigureAwait(false);

        if (batch is null)
        {
            throw new InvalidOperationException("Generation API returned an empty response.");
        }

        Logger.LogInformation(
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
