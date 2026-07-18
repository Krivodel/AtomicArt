using AtomicArt.Contracts.Generation;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Application.Features.Generation.Services;

public sealed class GenerationUsagePriceCalculator
{
    private const string ActualProviderUsageSource = "ActualProviderUsage";

    public GenerationPriceDto? Calculate(
        string modelId,
        GenerationModelPricingMetadataDto pricing,
        GenerationUsageDto? usage,
        string resolution,
        int generatedImageCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentNullException.ThrowIfNull(pricing);

        if (usage is null
            || string.IsNullOrWhiteSpace(resolution)
            || generatedImageCount <= 0
            || usage.TotalInputTokens is null
            || usage.TotalOutputTokens is null
            || usage.TotalInputTokens < 0
            || usage.TotalOutputTokens < 0
            || HasNegativeOptionalTokenCounts(usage)
            || HasUnpricedProviderTokenCounts(usage)
            || HasUnsupportedModality(
                usage.InputTokensByModality,
                GenerationUsageModalityNames.KnownImageGenerationInputModalities)
            || usage.TotalTokens <= 0
            || string.IsNullOrWhiteSpace(pricing.CurrencyCode))
        {
            return null;
        }

        OutputTokenCounts? outputTokenCounts = CalculateOutputTokenCounts(
            pricing,
            usage,
            resolution,
            generatedImageCount);

        if (outputTokenCounts is null)
        {
            return null;
        }

        GenerationModelPricing domainPricing = GenerationModelMetadataDomainMapper.ToPricing(modelId, pricing);
        decimal amount = CalculateDomainPrice(
            domainPricing,
            usage.TotalInputTokens.Value,
            outputTokenCounts);

        return new GenerationPriceDto(
            amount,
            pricing.CurrencyCode,
            ActualProviderUsageSource);
    }

    private static OutputTokenCounts? CalculateOutputTokenCounts(
        GenerationModelPricingMetadataDto pricing,
        GenerationUsageDto usage,
        string resolution,
        int generatedImageCount)
    {
        if (!pricing.OutputImageTokensByResolution.TryGetValue(
                resolution,
                out int imageOutputTokensPerImage))
        {
            return null;
        }

        if (imageOutputTokensPerImage <= 0)
        {
            return null;
        }

        long textOutputTokens = usage.TotalThoughtTokens ?? 0;
        long imageOutputTokens = (long)imageOutputTokensPerImage * generatedImageCount;

        if (HasUnsupportedModality(
                usage.OutputTokensByModality,
                GenerationUsageModalityNames.KnownImageGenerationOutputModalities))
        {
            return null;
        }

        if (usage.OutputTokensByModality is not null)
        {
            foreach (GenerationModalityTokensDto modalityTokens in usage.OutputTokensByModality)
            {
                string modality = modalityTokens.Modality.Trim().ToLowerInvariant();

                if (string.Equals(modality, GenerationUsageModalityNames.Text, StringComparison.Ordinal))
                {
                    textOutputTokens += modalityTokens.Tokens;

                    if (textOutputTokens > int.MaxValue)
                    {
                        return null;
                    }
                }
            }
        }

        return CreateOutputTokenCounts(textOutputTokens, imageOutputTokens);
    }

    private static bool HasUnsupportedModality(
        IReadOnlyList<GenerationModalityTokensDto>? modalityTokenCounts,
        IReadOnlyList<string> supportedModalities)
    {
        if (modalityTokenCounts is null)
        {
            return false;
        }

        foreach (GenerationModalityTokensDto modalityTokens in modalityTokenCounts)
        {
            if (modalityTokens.Tokens < 0 || string.IsNullOrWhiteSpace(modalityTokens.Modality))
            {
                return true;
            }

            string modality = modalityTokens.Modality.Trim().ToLowerInvariant();

            if (!supportedModalities.Contains(modality, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNegativeOptionalTokenCounts(GenerationUsageDto usage)
    {
        return usage.TotalThoughtTokens is < 0
            || usage.TotalToolUseTokens is < 0
            || usage.TotalCachedTokens is < 0;
    }

    private static bool HasUnpricedProviderTokenCounts(GenerationUsageDto usage)
    {
        return usage.TotalToolUseTokens is > 0
            || usage.TotalCachedTokens is > 0;
    }

    private static OutputTokenCounts? CreateOutputTokenCounts(
        long textOutputTokens,
        long imageOutputTokens)
    {
        if (textOutputTokens > int.MaxValue || imageOutputTokens > int.MaxValue)
        {
            return null;
        }

        return new OutputTokenCounts((int)textOutputTokens, (int)imageOutputTokens);
    }

    private static decimal CalculateDomainPrice(
        GenerationModelPricing domainPricing,
        int inputTokens,
        OutputTokenCounts outputTokenCounts)
    {
        return domainPricing.CalculateUsagePrice(
            inputTokens,
            outputTokenCounts.TextOutputTokens,
            outputTokenCounts.ImageOutputTokens);
    }

    private sealed record OutputTokenCounts(
        int TextOutputTokens,
        int ImageOutputTokens);
}
