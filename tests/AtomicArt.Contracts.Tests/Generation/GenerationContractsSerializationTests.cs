using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;

namespace AtomicArt.Contracts.Tests.Generation;

public sealed class GenerationContractsSerializationTests
{
    private const string ModelId = "nano-banana-2";
    private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 1, 12, 30, 45, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SerializeAndDeserialize_WithImageGenerationRequestDto_PreservesContractShape()
    {
        ImageGenerationRequestDto request = new(
            ModelId,
            "Create a city skyline",
            "16:9",
            "1k",
            1d,
            2,
            [
                new("reference.png", "image/png", [0x01, 0x02, 0x03])
            ],
            "high");

        string json = JsonSerializer.Serialize(request, JsonOptions);
        ImageGenerationRequestDto? deserialized = JsonSerializer.Deserialize<ImageGenerationRequestDto>(json, JsonOptions);

        json.Should().Contain("\"modelId\"");
        json.Should().Contain("\"temperature\":1");
        json.Should().Contain("\"thinkingLevel\":\"high\"");
        json.Should().Contain("\"attachedImages\"");
        deserialized.Should().BeEquivalentTo(request);
    }

    [Fact]
    public void SerializeAndDeserialize_WithGenerationBatchDto_PreservesContractShape()
    {
        GenerationBatchDto batch = new(
            BatchId,
            [
                new(
                    ItemId,
                    ModelId,
                    "Nano Banana 2",
                    "Create a city skyline",
                    "16:9",
                    "1k",
                    CreatedAtUtc,
                    GenerationItemStatus.Generated,
                    "images/result.png")
            ]);

        string json = JsonSerializer.Serialize(batch, JsonOptions);
        GenerationBatchDto? deserialized = JsonSerializer.Deserialize<GenerationBatchDto>(json, JsonOptions);

        json.Should().Contain("\"batchId\"");
        json.Should().Contain("\"createdAtUtc\"");
        deserialized.Should().BeEquivalentTo(batch);
    }

    [Fact]
    public void ContractsAssembly_WithPhaseFour_DoesNotContainGenerationQuoteDto()
    {
        IReadOnlyList<string> contractTypeNames = typeof(GenerationApiRoutes)
            .Assembly
            .GetTypes()
            .Select(type => type.Name)
            .ToList();

        contractTypeNames.Should().NotContain("GenerationQuoteDto");
    }

    [Fact]
    public void GenerationImageContentDto_WithValidValues_RoundTripsThroughJson()
    {
        GenerationImageContentDto content = new("image/png", "AQIDBA==");

        string json = JsonSerializer.Serialize(content, JsonOptions);
        GenerationImageContentDto? deserialized = JsonSerializer.Deserialize<GenerationImageContentDto>(json, JsonOptions);

        json.Should().Contain("\"contentType\"");
        json.Should().Contain("\"base64Data\"");
        deserialized.Should().BeEquivalentTo(content);
    }

    [Fact]
    public void GenerationItemDto_WithImageContent_SerializesContentFields()
    {
        GenerationImageContentDto content = new("image/png", "AQIDBA==");
        GenerationItemDto item = new(
            ItemId,
            ModelId,
            "Nano Banana 2",
            "Create a city skyline",
            "16:9",
            "1k",
            CreatedAtUtc,
            GenerationItemStatus.Generated,
            null,
            content);

        string json = JsonSerializer.Serialize(item, JsonOptions);
        GenerationItemDto? deserialized = JsonSerializer.Deserialize<GenerationItemDto>(json, JsonOptions);

        json.Should().Contain("\"imageContent\"");
        json.Should().NotContain("previewContent");
        json.Should().NotContain("previewPath");
        json.Should().Contain("\"contentType\":\"image/png\"");
        json.Should().Contain("\"base64Data\":\"AQIDBA==\"");
        deserialized.Should().BeEquivalentTo(item);
    }

    [Fact]
    public void GenerationItemDto_WithUsageAndPrice_RoundTripsThroughJson()
    {
        GenerationUsageDto usage = new(
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120,
            TotalTokens: 2320,
            InputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Text, 80),
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ],
            OutputTokensByModality:
            [
                new GenerationModalityTokensDto(GenerationUsageModalityNames.Image, 1120)
            ],
            TotalThoughtTokens: 0,
            TotalToolUseTokens: 0,
            TotalCachedTokens: 0);
        GenerationPriceDto price = new(
            0.0678m,
            "USD",
            GenerationPriceSources.ActualProviderUsage);
        GenerationItemDto item = new(
            ItemId,
            ModelId,
            "Nano Banana 2",
            "Create a city skyline",
            "16:9",
            "1k",
            CreatedAtUtc,
            GenerationItemStatus.Generated,
            null,
            new GenerationImageContentDto("image/png", "AQIDBA=="),
            CreatedAtUtc.AddSeconds(30),
            TimeSpan.FromSeconds(30),
            price,
            usage);

        string json = JsonSerializer.Serialize(item, JsonOptions);
        GenerationItemDto? deserialized = JsonSerializer.Deserialize<GenerationItemDto>(json, JsonOptions);

        json.Should().Contain("\"completedAtUtc\"");
        json.Should().Contain("\"generationDuration\"");
        json.Should().Contain("\"price\"");
        json.Should().Contain("\"usage\"");
        json.Should().Contain(
            $"\"source\":\"{GenerationPriceSources.ActualProviderUsage}\"");
        deserialized.Should().BeEquivalentTo(item);
    }

    [Fact]
    public void GenerationUsageDto_WithMissingOptionalFields_RoundTripsThroughJson()
    {
        GenerationUsageDto usage = new(
            TotalTokens: 2320,
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120);

        string json = JsonSerializer.Serialize(usage, JsonOptions);
        GenerationUsageDto? deserialized = JsonSerializer.Deserialize<GenerationUsageDto>(json, JsonOptions);

        json.Should().Contain("\"totalTokens\"");
        json.Should().Contain("\"totalInputTokens\"");
        json.Should().Contain("\"totalOutputTokens\"");
        json.Should().Contain("\"inputTokensByModality\":null");
        json.Should().Contain("\"outputTokensByModality\":null");
        json.Should().Contain("\"totalCachedTokens\":null");
        deserialized.Should().BeEquivalentTo(usage);
    }

    [Fact]
    public void GenerationItemDto_WithLegacyPaths_AllowsNullContentForCompatibility()
    {
        string json = """
        {
          "id": "22222222-2222-2222-2222-222222222222",
          "modelId": "nano-banana-2",
          "modelDisplayName": "Nano Banana 2",
          "prompt": "Create a city skyline",
          "aspectRatio": "16:9",
          "resolution": "1k",
          "createdAtUtc": "2026-07-01T12:30:45Z",
          "status": "Generated",
          "imagePath": "images/result.png"
        }
        """;

        GenerationItemDto? deserialized = JsonSerializer.Deserialize<GenerationItemDto>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        GenerationItemDto item = deserialized
            ?? throw new InvalidOperationException("Generation item must be deserialized.");
        item.ImagePath.Should().Be("images/result.png");
        item.Status.Should().Be(GenerationItemStatus.Generated);
        item.ImageContent.Should().BeNull();
        item.CompletedAtUtc.Should().BeNull();
        item.GenerationDuration.Should().BeNull();
        item.Price.Should().BeNull();
        item.Usage.Should().BeNull();
    }
}
