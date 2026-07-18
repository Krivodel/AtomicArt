using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.GoogleInteractions;

public sealed class GoogleInteractionsRequestBuilderTests
{
    [Fact]
    public void Build_WithNanoBanana2_DoesNotIncludeBackground()
    {
        GoogleInteractionsRequestBuilder builder = new();
        ImageGenerationContentProviderContext context = CreateContext();

        string json = builder.Build(context);

        json.Should().NotContain("background");
    }

    [Fact]
    public void Build_WithAttachedImage_AddsProviderModelAndSelectedParameters()
    {
        GoogleInteractionsRequestBuilder builder = new();
        ImageGenerationContentProviderContext context = CreateContext();

        string json = builder.Build(context);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        root.GetProperty("model").GetString().Should().Be(
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().ProviderModelId);
        root.GetProperty("system_instruction").GetString().Should().Be(
            "Treat **EVERY user input as an image generation request**. Return **image output only**. DO NOT answer with explanatory text.");
        root.TryGetProperty("response_modalities", out _).Should().BeFalse();
        root.GetProperty("generation_config")
            .GetProperty("temperature")
            .GetDouble()
            .Should()
            .Be(ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata().Temperature.Default);
        root.TryGetProperty("image_config", out _).Should().BeFalse();
        root.TryGetProperty("delivery", out _).Should().BeFalse();
        JsonElement responseFormat = root.GetProperty("response_format");
        responseFormat.GetProperty("type").GetString().Should().Be("image");
        responseFormat.GetProperty("mime_type").GetString().Should().Be("image/jpeg");
        responseFormat.GetProperty("aspect_ratio").GetString().Should().Be("1:1");
        responseFormat.GetProperty("image_size").GetString().Should().Be("1K");
        JsonElement input = root.GetProperty("input");
        input[0].GetProperty("text").GetString().Should().Be("Prompt");
        input[1].GetProperty("mime_type").GetString().Should().Be("image/png");
        byte[] expectedImageBytes = [0x01, 0x02];
        input[1].GetProperty("data").GetString().Should().Be(Convert.ToBase64String(expectedImageBytes));
    }

    [Fact]
    public void Build_WithAutoAspectRatio_OmitsAspectRatio()
    {
        GoogleInteractionsRequestBuilder builder = new();
        ImageGenerationContentProviderContext context = CreateContext(GenerationAspectRatios.Auto);

        string json = builder.Build(context);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement responseFormat = document.RootElement.GetProperty("response_format");
        responseFormat.TryGetProperty("aspect_ratio", out _).Should().BeFalse();
        responseFormat.GetProperty("image_size").GetString().Should().Be("1K");
    }

    [Theory]
    [InlineData("low")]
    [InlineData("high")]
    public void Build_WithThinkingLevel_AddsThinkingLevelToGenerationConfig(string thinkingLevel)
    {
        GoogleInteractionsRequestBuilder builder = new();
        ImageGenerationContentProviderContext context = CreateContext(thinkingLevel: thinkingLevel);

        string json = builder.Build(context);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("generation_config")
            .GetProperty("thinking_level")
            .GetString()
            .Should()
            .Be(thinkingLevel);
    }

    [Fact]
    public void Build_WithoutThinkingLevel_OmitsThinkingLevel()
    {
        GoogleInteractionsRequestBuilder builder = new();
        ImageGenerationContentProviderContext context = CreateContext();

        string json = builder.Build(context);

        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("generation_config")
            .TryGetProperty("thinking_level", out _)
            .Should()
            .BeFalse();
    }

    private static ImageGenerationContentProviderContext CreateContext(
        string aspectRatio = "1:1",
        string? thinkingLevel = null)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        ImageGenerationRequestDto request = new(
            metadata.Id,
            "Prompt",
            aspectRatio,
            "1K",
            metadata.Temperature.Default,
            1,
            [
                new(
                    "reference.png",
                    "image/png",
                    [0x01, 0x02])
            ],
            thinkingLevel);

        return new ImageGenerationContentProviderContext(
            request,
            GenerationProviderIds.Google,
            metadata.ProviderModelId,
            CreatePricing(),
            0,
            "test-provider-key");
    }

    private static GenerationModelPricingMetadataDto CreatePricing()
    {
        return new GenerationModelPricingMetadataDto(
            "USD",
            0.50m,
            3.00m,
            60.00m,
            1120,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["1K"] = 1120
            });
    }
}
