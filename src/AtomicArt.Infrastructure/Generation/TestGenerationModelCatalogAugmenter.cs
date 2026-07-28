using System.Text.Json;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation;

public static class TestGenerationModelCatalogAugmenter
{
    private const long Base64EnvelopeBytes = 1024L * 1024L;

    private static readonly IReadOnlyList<string> SupportedContentTypes =
        GenerationImageFileFormats.All
            .Select(format => format.ContentType)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(contentType => contentType, StringComparer.Ordinal)
            .ToArray();

    public static GenerationModelCatalogDto AddTestModelIfEnabled(
        GenerationModelCatalogDto catalog,
        TestGenerationOptions options,
        TestGenerationModelMetadata? testModelMetadata)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return catalog;
        }

        TestGenerationModelMetadata metadata = testModelMetadata
            ?? throw new InvalidOperationException(
                "Generation model metadata does not contain the Test model.");
        IReadOnlyList<GenerationModelMetadataDto> existingModels = catalog.Models ?? [];

        if (existingModels.Any(model => string.Equals(
                model.Id,
                metadata.Id,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Generation model catalog already contains the Test model.");
        }

        List<GenerationModelMetadataDto> models = existingModels.ToList();
        GenerationModelMetadataDto baseMetadata = existingModels
            .FirstOrDefault(model => string.Equals(
                model.PanelId,
                GenerationPanelIds.NanoBanana,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "Generation model catalog does not contain Nano Banana temperature metadata.");
        models.Add(CreateTestModel(baseMetadata, options, metadata));

        return new GenerationModelCatalogDto(models.AsReadOnly());
    }

    private static GenerationModelMetadataDto CreateTestModel(
        GenerationModelMetadataDto baseMetadata,
        TestGenerationOptions options,
        TestGenerationModelMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(baseMetadata);
        ArgumentNullException.ThrowIfNull(options);

        GenerationModelAttachmentMetadataDto attachments = baseMetadata.Attachments with
        {
            SupportedContentTypes = SupportedContentTypes
        };
        GenerationModelTransportLimitsDto baseTransportLimits =
            baseMetadata.TransportLimits
            ?? throw new InvalidOperationException(
                "Base generation model does not define transport limits.");
        GenerationModelTransportLimitsDto transportLimits =
            baseTransportLimits with
            {
                MaxResponseBytes = Math.Max(
                    baseTransportLimits.MaxResponseBytes,
                    CalculateMaximumResponseBytes(
                        options.MaxImageBytes)),
                AllowedResponseContentTypes = SupportedContentTypes
            };

        return baseMetadata with
        {
            Id = metadata.Id,
            DisplayName = metadata.DisplayName,
            Provider = GenerationProviderIds.Test,
            ProviderModelId = metadata.ProviderModelId,
            AspectRatios = metadata.AspectRatios,
            Resolutions = metadata.Resolutions,
            Attachments = attachments,
            Pricing = new GenerationModelPricingMetadataDto(
                "USD",
                InputTokenUsdPerMillion: 0m,
                TextOutputTokenUsdPerMillion: 0m,
                ImageOutputTokenUsdPerMillion: 0m,
                EstimatedCharactersPerTextToken:
                    baseMetadata.Pricing.EstimatedCharactersPerTextToken,
                InputImageTokens: 0,
                OutputImageTokensByResolution: metadata.Resolutions.ToDictionary(
                    resolution => resolution,
                    _ => 0,
                    StringComparer.Ordinal)),
            Thinking = null,
            Parameters = CreateTestParameters(
                baseMetadata,
                metadata),
            TransportLimits = transportLimits
        };
    }

    private static long CalculateMaximumResponseBytes(long maxImageBytes)
    {
        long maximumEncodableInputBytes =
            ((long.MaxValue - Base64EnvelopeBytes) / 4L) * 3L - 2L;

        if (maxImageBytes > maximumEncodableInputBytes)
        {
            return long.MaxValue;
        }

        long encodedImageBytes =
            ((maxImageBytes + 2L) / 3L) * 4L;

        return encodedImageBytes + Base64EnvelopeBytes;
    }

    private static IReadOnlyList<GenerationModelParameterMetadataDto> CreateTestParameters(
        GenerationModelMetadataDto baseMetadata,
        TestGenerationModelMetadata metadata)
    {
        GenerationModelParameterMetadataDto temperature =
            baseMetadata.Parameters?
                .Single(parameter => string.Equals(
                    parameter.Name,
                    GenerationParameterNames.Temperature,
                    StringComparison.Ordinal))
            ?? new GenerationModelParameterMetadataDto(
                GenerationParameterNames.Temperature,
                GenerationParameterTypes.Number,
                false,
                JsonSerializer.SerializeToElement(baseMetadata.Temperature.Default),
                baseMetadata.Temperature.Minimum,
                baseMetadata.Temperature.Maximum,
                baseMetadata.Temperature.Step);

        return new List<GenerationModelParameterMetadataDto>
        {
            temperature,
            new GenerationModelParameterMetadataDto(
                GenerationParameterNames.AspectRatio,
                GenerationParameterTypes.String,
                true,
                null,
                null,
                null,
                null,
                metadata.AspectRatios
                    .Select(value => JsonSerializer.SerializeToElement(value))
                    .ToList()),
            new GenerationModelParameterMetadataDto(
                GenerationParameterNames.Resolution,
                GenerationParameterTypes.String,
                true,
                null,
                null,
                null,
                null,
                metadata.Resolutions
                    .Select(value => JsonSerializer.SerializeToElement(value))
                    .ToList())
        };
    }
}
