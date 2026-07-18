using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation;

public static class TestGenerationModelCatalogAugmenter
{
    public const string ModelId = "test";
    private const string DisplayName = "Test";
    private const string ProviderModelId = "test-folder";

    private static readonly IReadOnlyList<string> AspectRatios =
    [
        GenerationAspectRatios.Auto,
        "1:1",
        "2:3",
        "3:2",
        "4:3",
        "9:16",
        "16:9"
    ];
    private static readonly IReadOnlyList<string> Resolutions = ["1K"];
    private static readonly IReadOnlyList<int> GenerationCounts = [1, 2, 3, 4];
    private static readonly IReadOnlyList<string> SupportedContentTypes =
        GenerationImageFileFormats.All
            .Select(format => format.ContentType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(contentType => contentType, StringComparer.Ordinal)
            .ToArray();

    public static GenerationModelCatalogDto AddTestModelIfEnabled(
        GenerationModelCatalogDto catalog,
        TestGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return catalog;
        }

        IReadOnlyList<GenerationModelMetadataDto> existingModels = catalog.Models ?? [];

        if (existingModels.Any(model => string.Equals(model.Id, ModelId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Generation model catalog already contains the Test model.");
        }

        List<GenerationModelMetadataDto> models = existingModels.ToList();
        GenerationModelTemperatureMetadataDto temperature = existingModels
            .FirstOrDefault(model => string.Equals(
                model.PanelId,
                GenerationPanelIds.NanoBanana,
                StringComparison.Ordinal))
            ?.Temperature
            ?? throw new InvalidOperationException(
                "Generation model catalog does not contain Nano Banana temperature metadata.");
        models.Add(CreateTestModel(temperature));

        return new GenerationModelCatalogDto(models.AsReadOnly());
    }

    private static GenerationModelMetadataDto CreateTestModel(
        GenerationModelTemperatureMetadataDto temperature)
    {
        ArgumentNullException.ThrowIfNull(temperature);

        return new GenerationModelMetadataDto(
            ModelId,
            DisplayName,
            GenerationProviderIds.Test,
            ProviderModelId,
            GenerationPanelIds.NanoBanana,
            ContextWindowTokens: 131072,
            MaxOutputTokens: 32768,
            MaxPromptLength: 131072,
            AspectRatios,
            Resolutions,
            GenerationCounts,
            temperature,
            new GenerationModelAttachmentMetadataDto(
                MaxCount: 14,
                MaxSingleFileBytes: 7340032,
                MaxTotalBytes: 524288000,
                SupportedContentTypes),
            new GenerationModelPricingMetadataDto(
                "USD",
                InputTokenUsdPerMillion: 0m,
                TextOutputTokenUsdPerMillion: 0m,
                ImageOutputTokenUsdPerMillion: 0m,
                InputImageTokens: 0,
                OutputImageTokensByResolution: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["1K"] = 0
                }));
    }
}
