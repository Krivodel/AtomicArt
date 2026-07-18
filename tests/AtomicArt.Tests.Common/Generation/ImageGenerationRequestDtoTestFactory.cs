using AtomicArt.Contracts.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class ImageGenerationRequestDtoTestFactory
{
    private const string DefaultPrompt = "Prompt";

    public static ImageGenerationRequestDto Create(
        string? modelId = null,
        string prompt = DefaultPrompt,
        string aspectRatio = GenerationAspectRatios.Auto,
        string? resolution = null,
        double temperature = 1d,
        int generationCount = 1,
        IReadOnlyList<AttachedImageDto>? attachedImages = null,
        string? thinkingLevel = null)
    {
        return new ImageGenerationRequestDto(
            modelId ?? ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            prompt,
            aspectRatio,
            resolution ?? TestGenerationOutputMetadata.GeneratedImageResolution,
            temperature,
            generationCount,
            attachedImages ?? new List<AttachedImageDto>(),
            thinkingLevel);
    }
}
