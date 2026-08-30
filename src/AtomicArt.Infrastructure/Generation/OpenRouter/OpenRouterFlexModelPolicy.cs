namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal static class OpenRouterFlexModelPolicy
{
    public const string GoogleAiStudioFlexProviderTag = "google-ai-studio/global/flex";

    private const string NanoBananaProPreviewModelId = "google/gemini-3-pro-image-preview";

    // OpenRouter currently publishes this exact Flex endpoint only for the Pro preview.
    private static readonly HashSet<string> FlexModelIds = new(StringComparer.Ordinal)
    {
        NanoBananaProPreviewModelId
    };

    public static string? GetChatCompletionProviderTag(string providerModelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerModelId);

        return FlexModelIds.Contains(providerModelId)
            ? GoogleAiStudioFlexProviderTag
            : null;
    }
}
