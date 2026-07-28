using FluentAssertions;
using Xunit;

using AtomicArt.Domain.Exceptions;
using AtomicArt.Domain.Generation;

namespace AtomicArt.Domain.Tests.Generation;

public sealed class GenerationModelPricingTests
{
    [Fact]
    public void CalculateUsagePrice_WithCachedInputTokens_UsesConfiguredMultiplier()
    {
        GenerationModelPricing pricing = CreatePricing();

        decimal price = pricing.CalculateUsagePrice(
            inputTokens: 1000,
            cachedInputTokens: 200,
            textOutputTokens: 0,
            imageOutputTokens: 1120);

        price.Should().Be(0.06761m);
    }

    [Fact]
    public void CalculateUsagePrice_WithCachedInputTokensGreaterThanInputTokens_ThrowsDomainException()
    {
        GenerationModelPricing pricing = CreatePricing();

        Action action = () => pricing.CalculateUsagePrice(
            inputTokens: 1000,
            cachedInputTokens: 1001,
            textOutputTokens: 0,
            imageOutputTokens: 1120);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Constructor_WithCachedInputTokenPriceMultiplierGreaterThanOne_ThrowsDomainException()
    {
        Action action = () => CreatePricing(cachedInputTokenPriceMultiplier: 1.01m);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    [Fact]
    public void Constructor_WithZeroCachedInputTokenPriceMultiplier_ThrowsDomainException()
    {
        Action action = () => CreatePricing(cachedInputTokenPriceMultiplier: 0m);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(GenerationErrorCodes.ModelRequestValidation);
    }

    private static GenerationModelPricing CreatePricing(
        decimal cachedInputTokenPriceMultiplier = 0.1m)
    {
        Dictionary<string, int> outputImageTokensByResolution = new(StringComparer.Ordinal)
        {
            ["1K"] = 1120
        };

        return new GenerationModelPricing(
            modelId: "test-model",
            currencyCode: "USD",
            inputTokenUsdPerMillion: 0.50m,
            cachedInputTokenPriceMultiplier: cachedInputTokenPriceMultiplier,
            textOutputTokenUsdPerMillion: 3.00m,
            imageOutputTokenUsdPerMillion: 60.00m,
            estimatedCharactersPerTextToken: 4m,
            inputImageTokens: 1120,
            outputImageTokensByResolution: outputImageTokensByResolution);
    }
}
