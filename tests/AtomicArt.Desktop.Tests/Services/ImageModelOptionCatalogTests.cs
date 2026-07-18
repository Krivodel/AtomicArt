using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ImageModelOptionCatalogTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("test\u0001model")]
    public void Initialize_WithInvalidModelId_ThrowsInvalidOperationException(string? modelId)
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto dto = CreateCatalog(modelId: modelId);

        Action act = () => catalog.Initialize(dto);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Test\u0001Model")]
    public void Initialize_WithInvalidDisplayName_ThrowsInvalidOperationException(string? displayName)
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto dto = CreateCatalog(displayName: displayName);

        Action act = () => catalog.Initialize(dto);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Initialize_WithPricingMetadata_PreservesPricingMetadata()
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto dto = CreateCatalog();

        catalog.Initialize(dto);

        ImageModelOption option = catalog.GetModels().Single();
        option.Provider.Should().Be("google");
        option.ProviderModelId.Should().Be("provider-test-model");
        option.PanelId.Should().Be(GenerationPanelIds.NanoBanana);
        option.ContextWindowTokens.Should().Be(1000);
        option.MaxOutputTokens.Should().Be(500);
        option.Temperature.Should().Be(new GenerationModelTemperatureMetadataDto(0.1d, 2d, 1d, 0.1d));
        option.Pricing.CurrencyCode.Should().Be("USD");
        option.Pricing.OutputImageTokensByResolution["1k"].Should().Be(1120);
    }

    private static GenerationModelCatalogDto CreateCatalog(
        string? modelId = "test-model",
        string? displayName = "Test Model")
    {
        string[] aspectRatios = ["авто"];

        return GenerationModelCatalogJsonTestFactory.CreateCatalog(
            modelId,
            displayName,
            aspectRatios);
    }
}
