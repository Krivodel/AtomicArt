using AtomicArt.Contracts.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class GenerationItemDtoTestFactory
{
    private static readonly Guid DefaultItemId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime DefaultCreatedAtUtc =
        new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    public static GenerationItemDto Create(
        Guid? id = null,
        string? modelId = null,
        string? modelDisplayName = null,
        string prompt = TestGenerationPrompts.Default,
        string aspectRatio = GenerationAspectRatios.Auto,
        string? resolution = null,
        DateTime? createdAtUtc = null,
        GenerationItemStatus status = GenerationItemStatus.Generated,
        string? imagePath = null,
        GenerationImageContentDto? imageContent = null,
        DateTime? completedAtUtc = null,
        TimeSpan? generationDuration = null,
        GenerationPriceDto? price = null,
        GenerationUsageDto? usage = null)
    {
        return new GenerationItemDto(
            id ?? DefaultItemId,
            modelId ?? ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            modelDisplayName ?? ApiModelMetadataTestCatalog.NanoBanana2DisplayName,
            prompt,
            aspectRatio,
            resolution ?? TestGenerationOutputMetadata.GeneratedImageResolution,
            createdAtUtc ?? DefaultCreatedAtUtc,
            status,
            imagePath,
            imageContent,
            completedAtUtc,
            generationDuration,
            price,
            usage);
    }
}
