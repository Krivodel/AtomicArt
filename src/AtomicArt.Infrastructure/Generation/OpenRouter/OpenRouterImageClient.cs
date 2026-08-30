using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterImageClient : IOpenRouterImageClient
{
    private const string AuthorizationHeaderName = "Authorization";
    private const int MaxLoggedErrorMessageCharacters = 512;

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterImageClient> _logger;
    private readonly OpenRouterImageFailureClassifier _failureClassifier;
    private readonly string _chatCompletionsPath;
    private readonly string _imagesPath;

    public OpenRouterImageClient(
        HttpClient httpClient,
        ILogger<OpenRouterImageClient> logger,
        OpenRouterImageFailureClassifier failureClassifier,
        IOptions<OpenRouterImageOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _failureClassifier = failureClassifier ?? throw new ArgumentNullException(nameof(failureClassifier));
        ArgumentNullException.ThrowIfNull(options);
        _chatCompletionsPath = options.Value.ChatCompletionsPath;
        _imagesPath = options.Value.ImagesPath;
    }

    public async Task<OpenRouterImageResponse> CreateImageAsync(
        HttpContent content,
        string providerCredential,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCredential);

        return await CreateAsync(content, providerCredential, _imagesPath, ct).ConfigureAwait(false);
    }

    public async Task<OpenRouterImageResponse> CreateChatCompletionAsync(
        HttpContent content,
        string providerCredential,
        CancellationToken ct)
    {
        return await CreateAsync(content, providerCredential, _chatCompletionsPath, ct).ConfigureAwait(false);
    }

    private async Task<OpenRouterImageResponse> CreateAsync(
        HttpContent content,
        string providerCredential,
        string path,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCredential);

        using HttpRequestMessage request = new(HttpMethod.Post, path)
        {
            Content = content
        };
        request.Headers.Add(AuthorizationHeaderName, $"Bearer {providerCredential.Trim()}");

        try
        {
            HttpResponseMessage response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                GoogleInteractionsErrorDiagnostics diagnostics =
                    await GoogleInteractionsErrorResponseReader
                        .ReadAsync(
                            response.Content,
                            MaxLoggedErrorMessageCharacters,
                            ct)
                        .ConfigureAwait(false);

                try
                {
                    ThrowProviderError(response.StatusCode, diagnostics);
                }
                finally
                {
                    response.Dispose();
                }
            }

            Stream responseStream = await response.Content
                .ReadAsStreamAsync(ct)
                .ConfigureAwait(false);

            return new OpenRouterImageResponse(response, responseStream);
        }
        catch (TaskCanceledException exception) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "OpenRouter Image API request timed out.");
            throw new OpenRouterImageException(
                ImageGenerationProviderFailureKind.Timeout,
                "The generation provider response timed out.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "OpenRouter Image API request failed before receiving a response.");
            throw new OpenRouterImageException(
                ImageGenerationProviderFailureKind.Unavailable,
                "The generation provider is temporarily unavailable.");
        }
    }

    private void ThrowProviderError(
        HttpStatusCode statusCode,
        GoogleInteractionsErrorDiagnostics diagnostics)
    {
        ImageGenerationProviderFailureKind failureKind = _failureClassifier.GetFailureKind(statusCode);
        _logger.LogWarning(
            "OpenRouter Image API request failed with HTTP status {StatusCode} mapped to provider failure {FailureKind}. Error body {ErrorBodyKind} with {ErrorBodyCharacterCount} characters; provider code {ProviderErrorCode}; provider status {ProviderErrorStatus}; provider message {ProviderErrorMessage}.",
            (int)statusCode,
            failureKind,
            diagnostics.BodyKind,
            diagnostics.CharacterCount,
            diagnostics.ErrorCode,
            diagnostics.ErrorStatus,
            diagnostics.ErrorMessage);

        throw new OpenRouterImageException(
            failureKind,
            "The generation provider returned an error.",
            _failureClassifier.IsRetryable(statusCode));
    }

}
