using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

internal static class GenerationModelMetadataDomainMapper
{
    public static GenerationModelConstraints ToConstraints(GenerationModelMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(metadata.Attachments);
        ArgumentNullException.ThrowIfNull(metadata.Temperature);

        return new GenerationModelConstraints(
            metadata.Id,
            metadata.MaxPromptLength,
            metadata.AspectRatios,
            metadata.Resolutions,
            metadata.GenerationCounts,
            new GenerationModelTemperatureConstraints(
                metadata.Temperature.Minimum,
                metadata.Temperature.Maximum,
                metadata.Temperature.Default,
                metadata.Temperature.Step),
            metadata.Attachments.MaxCount,
            metadata.Attachments.MaxSingleFileBytes,
            metadata.Attachments.MaxTotalBytes,
            metadata.Attachments.SupportedContentTypes,
            CreateThinkingConstraints(metadata.Thinking));
    }

    private static GenerationModelThinkingConstraints? CreateThinkingConstraints(
        GenerationModelThinkingMetadataDto? thinking)
    {
        if (thinking is null)
        {
            return null;
        }

        IReadOnlyList<string>? levels = thinking.Levels?
            .Select(level => level?.Value ?? string.Empty)
            .ToList();

        return new GenerationModelThinkingConstraints(levels, thinking.Default);
    }

    public static GenerationModelPricing ToPricing(
        string modelId,
        GenerationModelPricingMetadataDto pricing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(pricing);

        return new GenerationModelPricing(
            modelId,
            pricing.CurrencyCode,
            pricing.InputTokenUsdPerMillion,
            pricing.TextOutputTokenUsdPerMillion,
            pricing.ImageOutputTokenUsdPerMillion,
            pricing.InputImageTokens,
            pricing.OutputImageTokensByResolution);
    }
}
