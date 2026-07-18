using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsClient : IGoogleInteractionsClient
{
    private const string ApiKeyHeaderName = "X-Goog-Api-Key";
    private const string InteractionsPath = "/v1beta/interactions";
    private const string JsonMediaType = "application/json";
    private const int MaxInternalServerErrorRetryCount = 4;

    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleInteractionsClient> _logger;

    public GoogleInteractionsClient(
        HttpClient httpClient,
        ILogger<GoogleInteractionsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> CreateInteractionAsync(
        string requestJson,
        string providerCredential,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCredential);

        int attemptNumber = 1;
        Stopwatch stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Google Interactions API request started. Request size {RequestSize} characters.",
            requestJson.Length);

        while (true)
        {
            using HttpRequestMessage request = CreateRequest(requestJson, providerCredential);
            using HttpResponseMessage response = await SendAsync(request, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content
                    .ReadAsStringAsync(ct)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Google Interactions API request completed with HTTP status {StatusCode} after {ElapsedMilliseconds} ms. Response size {ResponseSize} characters.",
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    responseJson.Length);

                return responseJson;
            }

            GoogleInteractionsErrorDiagnostics diagnostics = await GoogleInteractionsErrorResponseReader
                .ReadAsync(response.Content, ct)
                .ConfigureAwait(false);

            if (CanRetryInternalServerError(response.StatusCode, attemptNumber))
            {
                _logger.LogWarning(
                    "Google Interactions API returned HTTP status {StatusCode}. Retrying request {RetryAttempt} of {MaxRetryAttempts}. Error body {ErrorBodyKind}; provider code {ProviderErrorCode}; provider status {ProviderErrorStatus}; provider message {ProviderErrorMessage}.",
                    (int)response.StatusCode,
                    attemptNumber,
                    MaxInternalServerErrorRetryCount,
                    diagnostics.BodyKind,
                    diagnostics.ErrorCode,
                    diagnostics.ErrorStatus,
                    diagnostics.ErrorMessage);

                attemptNumber++;
                continue;
            }

            ThrowProviderError(response.StatusCode, diagnostics, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        try
        {
            return await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException exception) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Google Interactions API request timed out.");

            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.Timeout,
                "The generation provider response timed out.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Google Interactions API request failed before receiving a response.");

            throw new GoogleInteractionsException(
                ImageGenerationProviderFailureKind.Unavailable,
                "The generation provider is temporarily unavailable.");
        }
    }

    private HttpRequestMessage CreateRequest(
        string requestJson,
        string providerCredential)
    {
        HttpRequestMessage request = new(
            HttpMethod.Post,
            InteractionsPath);
        request.Headers.Add(ApiKeyHeaderName, providerCredential.Trim());
        request.Content = new StringContent(requestJson, Encoding.UTF8, JsonMediaType);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonMediaType);

        return request;
    }

    private static bool CanRetryInternalServerError(
        HttpStatusCode statusCode,
        int attemptNumber)
    {
        return statusCode == HttpStatusCode.InternalServerError
            && attemptNumber <= MaxInternalServerErrorRetryCount;
    }

    private void ThrowProviderError(
        HttpStatusCode statusCode,
        GoogleInteractionsErrorDiagnostics diagnostics,
        long elapsedMilliseconds)
    {
        ImageGenerationProviderFailureKind failureKind = statusCode switch
        {
            HttpStatusCode.BadRequest => ImageGenerationProviderFailureKind.RequestRejected,
            HttpStatusCode.Unauthorized => ImageGenerationProviderFailureKind.Authentication,
            HttpStatusCode.Forbidden => ImageGenerationProviderFailureKind.Authorization,
            HttpStatusCode.NotFound => ImageGenerationProviderFailureKind.ResourceNotFound,
            (HttpStatusCode)429 => ImageGenerationProviderFailureKind.RateLimited,
            HttpStatusCode.InternalServerError => ImageGenerationProviderFailureKind.InternalError,
            HttpStatusCode.BadGateway => ImageGenerationProviderFailureKind.InvalidResponse,
            HttpStatusCode.ServiceUnavailable => ImageGenerationProviderFailureKind.Unavailable,
            HttpStatusCode.GatewayTimeout => ImageGenerationProviderFailureKind.Timeout,
            _ => ImageGenerationProviderFailureKind.Unknown
        };

        _logger.LogWarning(
            "Google Interactions API request failed with HTTP status {StatusCode} mapped to provider failure {FailureKind} after {ElapsedMilliseconds} ms. Error body {ErrorBodyKind} with {ErrorBodyCharacterCount} characters; provider code {ProviderErrorCode}; provider status {ProviderErrorStatus}; provider message {ProviderErrorMessage}.",
            (int)statusCode,
            failureKind,
            elapsedMilliseconds,
            diagnostics.BodyKind,
            diagnostics.CharacterCount,
            diagnostics.ErrorCode,
            diagnostics.ErrorStatus,
            diagnostics.ErrorMessage);

        throw new GoogleInteractionsException(
            failureKind,
            "The generation provider returned an error.");
    }
}
