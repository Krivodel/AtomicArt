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

        (string Json, ImageGenerationRequestDto? Deserialized) result = RoundTrip(request);

        result.Json.Should().Contain("\"modelId\"");
        result.Json.Should().Contain("\"temperature\":1");
        result.Json.Should().Contain("\"thinkingLevel\":\"high\"");
        result.Json.Should().Contain("\"attachedImages\"");
        result.Deserialized.Should().BeEquivalentTo(request);
    }

    [Fact]
    public void SerializeAndDeserialize_WithGenerationBatchDto_PreservesContractShape()
    {
        GenerationBatchDto batch = new(
            BatchId,
            [
                CreateGenerationItem(imagePath: "images/result.png")
            ]);

        (string Json, GenerationBatchDto? Deserialized) result = RoundTrip(batch);

        result.Json.Should().Contain("\"batchId\"");
        result.Json.Should().Contain("\"createdAtUtc\"");
        result.Deserialized.Should().BeEquivalentTo(batch);
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

        (string Json, GenerationImageContentDto? Deserialized) result = RoundTrip(content);

        result.Json.Should().Contain("\"contentType\"");
        result.Json.Should().Contain("\"base64Data\"");
        result.Deserialized.Should().BeEquivalentTo(content);
    }

    [Fact]
    public void GenerationItemDto_WithImageContent_SerializesContentFields()
    {
        GenerationImageContentDto content = new("image/png", "AQIDBA==");
        GenerationItemDto item = CreateGenerationItem(imageContent: content);

        (string Json, GenerationItemDto? Deserialized) result = RoundTrip(item);

        result.Json.Should().Contain("\"imageContent\"");
        result.Json.Should().NotContain("previewContent");
        result.Json.Should().NotContain("previewPath");
        result.Json.Should().Contain("\"contentType\":\"image/png\"");
        result.Json.Should().Contain("\"base64Data\":\"AQIDBA==\"");
        result.Deserialized.Should().BeEquivalentTo(item);
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
        GenerationItemDto item = CreateGenerationItem(
            imageContent: new GenerationImageContentDto("image/png", "AQIDBA=="),
            completedAtUtc: CreatedAtUtc.AddSeconds(30),
            generationDuration: TimeSpan.FromSeconds(30),
            price: price,
            usage: usage);

        (string Json, GenerationItemDto? Deserialized) result = RoundTrip(item);

        result.Json.Should().Contain("\"completedAtUtc\"");
        result.Json.Should().Contain("\"generationDuration\"");
        result.Json.Should().Contain("\"price\"");
        result.Json.Should().Contain("\"usage\"");
        result.Json.Should().Contain(
            $"\"source\":\"{GenerationPriceSources.ActualProviderUsage}\"");
        result.Deserialized.Should().BeEquivalentTo(item);
    }

    [Fact]
    public void GenerationUsageDto_WithMissingOptionalFields_RoundTripsThroughJson()
    {
        GenerationUsageDto usage = new(
            TotalTokens: 2320,
            TotalInputTokens: 1200,
            TotalOutputTokens: 1120);

        (string Json, GenerationUsageDto? Deserialized) result = RoundTrip(usage);

        result.Json.Should().Contain("\"totalTokens\"");
        result.Json.Should().Contain("\"totalInputTokens\"");
        result.Json.Should().Contain("\"totalOutputTokens\"");
        result.Json.Should().Contain("\"inputTokensByModality\":null");
        result.Json.Should().Contain("\"outputTokensByModality\":null");
        result.Json.Should().Contain("\"totalCachedTokens\":null");
        result.Deserialized.Should().BeEquivalentTo(usage);
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

    private static GenerationItemDto CreateGenerationItem(
        string? imagePath = null,
        GenerationImageContentDto? imageContent = null,
        DateTime? completedAtUtc = null,
        TimeSpan? generationDuration = null,
        GenerationPriceDto? price = null,
        GenerationUsageDto? usage = null)
    {
        return new GenerationItemDto(
            ItemId,
            ModelId,
            "Nano Banana 2",
            "Create a city skyline",
            "16:9",
            "1k",
            CreatedAtUtc,
            GenerationItemStatus.Generated,
            imagePath,
            imageContent,
            completedAtUtc,
            generationDuration,
            price,
            usage);
    }

    private static (string Json, T? Deserialized) RoundTrip<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        T? deserialized = JsonSerializer.Deserialize<T>(json, JsonOptions);

        return (json, deserialized);
    }
}
