using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleInteractionsClient : IGoogleInteractionsClient
{
    private const string ApiKeyHeaderName = "X-Goog-Api-Key";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleInteractionsClient> _logger;
    private readonly GoogleInteractionsFailureClassifier _failureClassifier;
    private readonly string _interactionsPath;
    private readonly int _maxLoggedErrorMessageCharacters;

    public GoogleInteractionsClient(
        HttpClient httpClient,
        ILogger<GoogleInteractionsClient> logger,
        GoogleInteractionsFailureClassifier failureClassifier,
        IOptions<GoogleInteractionsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(failureClassifier);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _logger = logger;
        _failureClassifier = failureClassifier;
        _interactionsPath = options.Value.InteractionsPath;
        _maxLoggedErrorMessageCharacters =
            options.Value.MaxLoggedErrorMessageCharacters;
    }

    public async Task<GoogleInteractionsStreamingResponse> CreateInteractionStreamAsync(
        HttpContent content,
        string providerCredential,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCredential);

        HttpRequestMessage request = CreateRequest(content, providerCredential);

        try
        {
            HttpResponseMessage response = await SendAsync(request, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                GoogleInteractionsErrorDiagnostics diagnostics =
                    await GoogleInteractionsErrorResponseReader
                        .ReadAsync(
                            response.Content,
                            _maxLoggedErrorMessageCharacters,
                            ct)
                        .ConfigureAwait(false);

                try
                {
                    ThrowProviderError(response.StatusCode, diagnostics, 0L);
                }
                finally
                {
                    response.Dispose();
                }
            }

            Stream responseStream = await response.Content
                .ReadAsStreamAsync(ct)
                .ConfigureAwait(false);
            request.Dispose();

            return new GoogleInteractionsStreamingResponse(
                response,
                responseStream);
        }
        catch
        {
            request.Dispose();
            throw;
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
        HttpContent content,
        string providerCredential)
    {
        HttpRequestMessage request = new(
            HttpMethod.Post,
            _interactionsPath);
        request.Headers.Add(ApiKeyHeaderName, providerCredential.Trim());
        request.Content = content;

        return request;
    }

    private void ThrowProviderError(
        HttpStatusCode statusCode,
        GoogleInteractionsErrorDiagnostics diagnostics,
        long elapsedMilliseconds)
    {
        ImageGenerationProviderFailureKind failureKind =
            _failureClassifier.GetFailureKind(statusCode);

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
            "The generation provider returned an error.",
            _failureClassifier.IsRetryable(statusCode));
    }
}
