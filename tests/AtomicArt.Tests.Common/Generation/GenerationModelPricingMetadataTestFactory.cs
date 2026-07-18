using AtomicArt.Contracts.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class GenerationModelPricingMetadataTestFactory
{
    public static GenerationModelPricingMetadataDto CreateCatalogPricing(
        IReadOnlyDictionary<string, int> outputImageTokensByResolution)
    {
        ArgumentNullException.ThrowIfNull(outputImageTokensByResolution);

        return new GenerationModelPricingMetadataDto(
            "USD",
            0.25m,
            1.50m,
            30.00m,
            1120,
            outputImageTokensByResolution);
    }

    public static GenerationModelPricingMetadataDto CreateProviderPricing(
        IReadOnlyDictionary<string, int> outputImageTokensByResolution)
    {
        ArgumentNullException.ThrowIfNull(outputImageTokensByResolution);

        return new GenerationModelPricingMetadataDto(
            "USD",
            0.50m,
            3.00m,
            60.00m,
            1120,
            outputImageTokensByResolution);
    }

    public static GenerationModelPricingMetadataDto CreateFreePricing()
    {
        return new GenerationModelPricingMetadataDto(
            "USD",
            0m,
            0m,
            0m,
            0,
            new Dictionary<string, int>());
    }
}
