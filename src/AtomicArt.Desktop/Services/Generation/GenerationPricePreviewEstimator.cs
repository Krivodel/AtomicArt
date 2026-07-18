using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationPricePreviewEstimator
{
    public const string Source = "EstimatedModelMetadata";

    private const decimal TokenPriceUnit = 1_000_000m;
    private const decimal CharactersPerTextToken = 4m;

    public GenerationPriceDto? Estimate(NanoBanana2GenerationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        GenerationModelPricingMetadataDto pricing = parameters.SelectedModel.Pricing;

        if (parameters.GenerationCount <= 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(parameters.Resolution)
            || !pricing.OutputImageTokensByResolution.TryGetValue(
                parameters.Resolution.Trim(),
                out int outputImageTokens))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(pricing.CurrencyCode))
        {
            return null;
        }

        decimal inputTokens = EstimatePromptTokens(parameters.Prompt)
            + ((decimal)parameters.AttachedImages.Count * pricing.InputImageTokens);
        decimal outputTokens = (decimal)parameters.GenerationCount * outputImageTokens;
        decimal amount = CalculateTokenPrice(inputTokens, pricing.InputTokenUsdPerMillion)
            + CalculateTokenPrice(outputTokens, pricing.ImageOutputTokenUsdPerMillion);

        return new GenerationPriceDto(amount, pricing.CurrencyCode, Source);
    }

    private static decimal EstimatePromptTokens(string? prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            return 0m;
        }

        return Math.Ceiling(prompt.Length / CharactersPerTextToken);
    }

    private static decimal CalculateTokenPrice(decimal tokens, decimal usdPerMillion)
    {
        return tokens * usdPerMillion / TokenPriceUnit;
    }
}
