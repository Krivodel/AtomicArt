using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ImageModelOptionCatalogTests
{
    [Fact]
    public void Initialize_WithRealMetadata_MakesNanoBanana21SelectableAndVisibilityConfigurable()
    {
        ImageModelOptionCatalog catalog = new();
        GenerationModelCatalogDto dto = ApiModelMetadataTestCatalog.LoadCatalog();
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadOpenRouterNanoBanana21Metadata();
        GenerationModelVisibilityService visibilityService = new();

        catalog.Initialize(dto);

        ImageModelOption option = catalog.GetModels().Single(model => model.Id == metadata.Id);
        option.ProviderModelId.Should().Be(metadata.ProviderModelId);
        option.Resolutions.Should().Equal(metadata.Resolutions);
        option.Thinking.Should().BeEquivalentTo(metadata.Thinking);
        option.SupportsTemperature.Should().BeFalse();
        GenerationModelVisibilitySettingDefinition.SupportedModelIds.Should().Contain(metadata.Id);
        visibilityService.IsVisible(metadata.Id).Should().BeTrue();

        visibilityService.SetVisibleModelIds(Array.Empty<string>());

        visibilityService.IsVisible(metadata.Id).Should().BeFalse();

        visibilityService.ApplySerializedValue(JsonSerializer.Serialize(new[] { metadata.Id }));

        visibilityService.IsVisible(metadata.Id).Should().BeTrue();
        visibilityService.GetVisibleModelIds().Should().ContainSingle().Which.Should().Be(metadata.Id);
    }

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

    [Fact]
    public void CatalogChanged_IsRaisedWhenCatalogIsClearedAndInitialized()
    {
        ImageModelOptionCatalog catalog = new();
        int changeCount = 0;
        catalog.CatalogChanged += (_, _) => changeCount++;

        catalog.Initialize(CreateCatalog());
        catalog.Clear();

        changeCount.Should().Be(2);
        catalog.IsLoaded.Should().BeFalse();
        catalog.GetModels().Should().BeEmpty();
    }

    private static GenerationModelCatalogDto CreateCatalog(
        string? modelId = "test-model",
        string? displayName = "Test Model")
    {
        string[] aspectRatios = [GenerationAspectRatios.Auto];

        return GenerationModelCatalogJsonTestFactory.CreateCatalog(
            modelId,
            displayName,
            aspectRatios);
    }
}
