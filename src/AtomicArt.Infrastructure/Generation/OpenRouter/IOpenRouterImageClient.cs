namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal interface IOpenRouterImageClient
{
    Task<OpenRouterImageResponse> CreateChatCompletionAsync(
        HttpContent content,
        string providerCredential,
        CancellationToken ct);

    Task<OpenRouterImageResponse> CreateImageAsync(
        HttpContent content,
        string providerCredential,
        CancellationToken ct);
}
