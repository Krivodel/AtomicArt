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
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
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

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.1364m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithMissingUsage_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            null,
            OneKResolution,
            1);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithMissingOutputModalityBreakdown_UsesResolutionImageTokenPrice()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithFourKImageUsage_UsesResolutionImageTokens()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            FourKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.1518m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithMultipleGeneratedImages_UsesResolutionImageTokensForEachImage()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            FourKResolution,
            2);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.303m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithTextImageAndThoughtOutput_UsesTextTariffAndResolutionImageTariff()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
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

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            FourKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.15395m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithInputTokenModalityBreakdown_UsesTotalInputTokensIncludingAttachedImages()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 2320,
            TotalOutputTokens: 1120,
            TotalTokens: 3440,
            InputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, 1200),
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ],
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ]);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.06836m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithUnknownInputModality_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 2320,
            TotalOutputTokens: 1120,
            TotalTokens: 3440,
            InputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, 1200),
                new GenerationModalityTokensDto("audio", 1120)
            ],
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ]);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithUnknownOutputModality_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
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

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            FourKResolution,
            1);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithToolUseTokens_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1000,
            TotalOutputTokens: 1120,
            TotalTokens: 2170,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ],
            TotalToolUseTokens: 50);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithCachedTokens_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1000,
            TotalOutputTokens: 1120,
            TotalTokens: 2120,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ],
            TotalCachedTokens: 200);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithZeroTotalTokens_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 0);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithTotalTokensGreaterThanKnownComponents_UsesAvailableUsageBreakdown()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2321);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithUnknownResolution_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = GenerationUsageTestFactory.CreateNanoBananaImageUsage();

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            "8K",
            1);

        price.Should().BeNull();
    }

    [Fact]
    public void Calculate_WithOutputBreakdownImageTokensBelowTotalOutputTokens_UsesResolutionImageTokens()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1119)
            ]);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage));
    }

    [Fact]
    public void Calculate_WithTextOutputBreakdownOverflow_ReturnsUnavailableResult()
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        GenerationUsageDto usage = new(
            TotalInputTokens: 1,
            TotalOutputTokens: 2,
            TotalTokens: 3,
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, int.MaxValue),
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, 1)
            ]);

        GenerationPriceDto? price = _calculator.Calculate(
            metadata.Id,
            metadata.Pricing,
            usage,
            OneKResolution,
            1);

        price.Should().BeNull();
    }
}
