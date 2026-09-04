using Microsoft.Extensions.Options;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterStreamingImageGenerationProvider : IProviderStreamingImageGenerationProvider
{
    public string Provider => GenerationProviderIds.OpenRouter;

    private readonly IOpenRouterImageClient _client;
    private readonly OpenRouterImageOptions _options;

    public OpenRouterStreamingImageGenerationProvider(
        IOpenRouterImageClient client,
        IOptions<OpenRouterImageOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task<IProviderGenerationStream> CreateStreamAsync(
        StreamingGenerationProviderContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(context.Provider, Provider, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(context.ProviderCredential))
        {
            throw new OpenRouterImageException(
                ImageGenerationProviderFailureKind.Authentication,
                "The temporary provider credential was not supplied.");
        }

        string? flexProviderTag = OpenRouterFlexModelPolicy
            .GetChatCompletionProviderTag(context.ProviderModelId);
        bool usesChatCompletions = !string.Equals(
            context.ProviderModelId,
            "openai/gpt-image-2",
            StringComparison.Ordinal);
        HttpContent content = usesChatCompletions
            ? new OpenRouterChatCompletionRequestContent(context, flexProviderTag)
            : new OpenRouterImageRequestContent(context);
        long maximumRequestBytes = context.TransportLimits is null
            ? _options.MaxRequestBytes
            : Math.Min(_options.MaxRequestBytes, context.TransportLimits.MaxRequestBytes);

        if (content.Headers.ContentLength > maximumRequestBytes)
        {
            content.Dispose();
            throw new OpenRouterImageException(
                ImageGenerationProviderFailureKind.RequestRejected,
                "The provider request exceeds the configured provider limit.");
        }

        try
        {
            OpenRouterImageResponse response = await CreateResponseAsync(
                    content,
                    usesChatCompletions,
                    context.ProviderCredential,
                    ct)
                .ConfigureAwait(false);
            long maximumResponseBytes = context.TransportLimits is null
                ? _options.MaxResponseBytes
                : Math.Min(_options.MaxResponseBytes, context.TransportLimits.MaxResponseBytes);

            return new OpenRouterProviderGenerationStream(
                response,
                maximumResponseBytes,
                _options.ResponseBufferSize,
                usesChatCompletions);
        }
        catch
        {
            content.Dispose();
            throw;
        }
    }

    private Task<OpenRouterImageResponse> CreateResponseAsync(
        HttpContent content,
        bool usesChatCompletions,
        string? providerCredential,
        CancellationToken ct)
    {
        return usesChatCompletions
            ? _client.CreateChatCompletionAsync(content, providerCredential!, ct)
            : _client.CreateImageAsync(content, providerCredential!, ct);
    }

}
