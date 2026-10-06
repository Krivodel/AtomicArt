using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal static class OpenRouterImageModelPolicy
{
    private static readonly HashSet<string> ImageApiModelIds = new(StringComparer.Ordinal)
    {
        GenerationProviderModelIds.NanoBanana21
    };

    public static bool UsesImageApi(string providerModelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModelId);

        return GenerationProviderModelIds.IsGptImageModel(providerModelId)
            || ImageApiModelIds.Contains(providerModelId);
    }
}
