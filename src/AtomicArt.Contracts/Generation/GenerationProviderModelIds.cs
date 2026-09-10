namespace AtomicArt.Contracts.Generation;

public static class GenerationProviderModelIds
{
    public const string GptImage2 = "openai/gpt-image-2";
    public const string GptImage25Sunburst = "openai/gpt-image-2.5-sunburst";
    public const string GptImage25Flare = "openai/gpt-image-2.5-flare";

    public static bool IsGptImageModel(string? providerModelId)
    {
        return string.Equals(providerModelId, GptImage2, StringComparison.Ordinal)
            || string.Equals(
                providerModelId,
                GptImage25Sunburst,
                StringComparison.Ordinal)
            || string.Equals(
                providerModelId,
                GptImage25Flare,
                StringComparison.Ordinal);
    }
}
