using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Application.Tests.Generation;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Application.Tests.Features.Generation.Services;

public sealed class GenerationUsagePriceCalculatorTests
{
    private const string OneKResolution = "1K";
    private const string FourKResolution = "4K";

    private readonly GenerationUsagePriceCalculator _calculator = new();

    [Fact]
    public void Calculate_WithNanoBanana2Usage_ReturnsMoneyAmount()
    {
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        AssertActualPrice(price, 0.0678m);
    }

    [Fact]
    public void Calculate_WithNanoBananaProUsage_UsesResolutionImageTokens()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1000,
            TotalOutputTokens: 2000,
            TotalTokens: 3000,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 2000)
            ]);

        GenerationPriceDto? price = Calculate(metadata, usage);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.1364m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithMissingUsage_ReturnsUnavailableResult()
    {
        GenerationPriceDto? price = CalculateNanoBanana2(null);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithMissingOutputModalityBreakdown_UsesResolutionImageTokenPrice()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        AssertActualPrice(price, 0.0678m);
    }

    [Fact]
    public void Calculate_WithFourKImageUsage_UsesResolutionImageTokens()
    {
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = CalculateNanoBanana2(usage, FourKResolution);

        AssertActualPrice(price, 0.1518m);
    }

    [Fact]
    public void Calculate_WithMultipleGeneratedImages_UsesResolutionImageTokensForEachImage()
    {
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = CalculateNanoBanana2(
            usage,
            FourKResolution,
            generatedImageCount: 2);

        AssertActualPrice(price, 0.303m);
    }

    [Fact]
    public void Calculate_WithTextImageAndThoughtOutput_UsesTextTariffAndResolutionImageTariff()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1000,
            TotalOutputTokens: 2500,
            TotalTokens: 3800,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(" IMAGE ", 2000),
                new GenerationModalityTokensDto(" TeXt ", 500)
            ],
            TotalThoughtTokens: 250,
            TotalToolUseTokens: 0);

        GenerationPriceDto? price = CalculateNanoBanana2(usage, FourKResolution);

        AssertActualPrice(price, 0.15395m);
    }

    [Fact]
    public void Calculate_WithInputTokenModalityBreakdown_UsesTotalInputTokensIncludingAttachedImages()
    {
        GenerationUsageDto usage = CreateInputModalityUsage(GenerationUsageModalityNames.Image);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        AssertActualPrice(price, 0.06836m);
    }

    [Fact]
    public void Calculate_WithUnknownInputModality_ReturnsUnavailableResult()
    {
        GenerationUsageDto usage = CreateInputModalityUsage("audio");

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithUnknownOutputModality_ReturnsUnavailableResult()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1000,
            TotalOutputTokens: 2600,
            TotalTokens: 3600,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120),
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, 500),
                new GenerationModalityTokensDto("audio", 1000)
            ],
            TotalThoughtTokens: 250);

        GenerationPriceDto? price = CalculateNanoBanana2(usage, FourKResolution);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithToolUseTokens_ReturnsUnavailableResult()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1000,
            TotalOutputTokens: 1120,
            TotalTokens: 2170,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ],
            TotalToolUseTokens: 50);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithCachedTokens_ReturnsUnavailableResult()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1000,
            TotalOutputTokens: 1120,
            TotalTokens: 2120,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ],
            TotalCachedTokens: 200);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithZeroTotalTokens_ReturnsUnavailableResult()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 0);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithTotalTokensGreaterThanKnownComponents_UsesAvailableUsageBreakdown()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2321);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        AssertActualPrice(price, 0.0678m);
    }

    [Fact]
    public void Calculate_WithUnknownResolution_ReturnsUnavailableResult()
    {
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = CalculateNanoBanana2(usage, "8K");

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithOutputBreakdownImageTokensBelowTotalOutputTokens_UsesResolutionImageTokens()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1119)
            ]);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        AssertActualPrice(price, 0.0678m);
    }

    [Fact]
    public void Calculate_WithTextOutputBreakdownOverflow_ReturnsUnavailableResult()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1,
            TotalOutputTokens: 2,
            TotalTokens: 3,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, int.MaxValue),
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, 1)
            ]);

        GenerationPriceDto? price = CalculateNanoBanana2(usage);

        price.Should().BeNull();
    }

    private static GenerationUsageDto CreateInputModalityUsage(string additionalInputModality)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(additionalInputModality);

        return new GenerationUsageDto(
            TotalInputTokens: 2320,
            TotalOutputTokens: 1120,
            TotalTokens: 3440,
            InputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, 1200),
                new GenerationModalityTokensDto(additionalInputModality, 1120)
            ],
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ]);
    }

    private static void AssertActualPrice(GenerationPriceDto? price, decimal amount)
    {
        price.Should().BeEquivalentTo(new GenerationPriceDto(
            amount,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    private GenerationPriceDto? CalculateNanoBanana2(
        GenerationUsageDto? usage,
        string resolution = OneKResolution,
        int generatedImageCount = 1)
    {
        return Calculate(
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata(),
            usage,
            resolution,
            generatedImageCount);
    }

    private GenerationPriceDto? Calculate(
        GenerationModelMetadataDto metadata,
        GenerationUsageDto? usage,
        string resolution = OneKResolution,
        int generatedImageCount = 1)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            resolution,
            generatedImageCount);
    }
}
