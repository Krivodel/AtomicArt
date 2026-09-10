using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.OpenRouter;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

public sealed class OpenRouterImageRequestContentTests
{
    [Theory]
    [InlineData("openrouter-gpt-image-2", "openai/gpt-image-2")]
    [InlineData("openrouter-gpt-image-2-5-sunburst", "openai/gpt-image-2.5-sunburst")]
    [InlineData("openrouter-gpt-image-2-5-flare", "openai/gpt-image-2.5-flare")]
    public async Task ReadAsStringAsync_WithGptImageModel_UsesSizeWithSelectedPixelBudget(
        string modelId,
        string providerModelId)
    {
        using OpenRouterImageRequestContent content = new(CreateContext(
            modelId,
            providerModelId,
            "2K",
            "16:9"));

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());
        JsonElement root = document.RootElement;

        root.GetProperty("size").GetString().Should().Be("2560x1440");
        root.TryGetProperty("resolution", out _).Should().BeFalse();
        root.TryGetProperty("aspect_ratio", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsStringAsync_WithGptImageModelQuality_SendsQuality()
    {
        using OpenRouterImageRequestContent content = new(CreateContext(
            "openrouter-gpt-image-2",
            "openai/gpt-image-2",
            "1K",
            "1:1",
            "high"));

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());

        document.RootElement.GetProperty("quality").GetString().Should().Be("high");
    }

    [Fact]
    public async Task ReadAsStringAsync_WithGptImageModelAndAutomaticAspectRatio_Uses16By9FallbackWithoutAspectRatio()
    {
        using OpenRouterImageRequestContent content = new(CreateContext(
            "openrouter-gpt-image-2",
            "openai/gpt-image-2",
            "4K",
            "auto"));

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());
        JsonElement root = document.RootElement;

        root.GetProperty("size").GetString().Should().Be("3840x2160");
        root.TryGetProperty("aspect_ratio", out _).Should().BeFalse();
        root.TryGetProperty("resolution", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsStringAsync_WithAutomaticAspectRatio_UsesFirstAttachmentRatio()
    {
        TestAttachmentSource firstAttachment = new(
            [1, 2, 3, 4],
            pixelWidth: 1600,
            pixelHeight: 800);
        TestAttachmentSource secondAttachment = new(
            [5, 6, 7, 8],
            pixelWidth: 900,
            pixelHeight: 1600);
        StreamingGenerationProviderContext requestContext = CreateContext(
            firstAttachment,
            secondAttachment);
        using OpenRouterImageRequestContent content = new(requestContext);

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());

        document.RootElement.GetProperty("size").GetString().Should().Be("1456x720");
    }

    [Fact]
    public async Task ReadAsStringAsync_WithNanoBanana_UsesModelAndResolution()
    {
        using OpenRouterImageRequestContent content = new(CreateContext(
            "openrouter-nano-banana-2",
            "google/gemini-3.1-flash-image",
            "4K",
            "auto"),
            "google-ai-studio");

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());
        JsonElement root = document.RootElement;

        root.GetProperty("model")
            .GetString()
            .Should()
            .Be("google/gemini-3.1-flash-image");
        root.GetProperty("resolution").GetString().Should().Be("4K");
        root.TryGetProperty("aspect_ratio", out _).Should().BeFalse();
        root.GetProperty("provider")
            .GetProperty("only")[0]
            .GetString()
            .Should()
            .Be("google-ai-studio");
        root.GetProperty("service_tier")
            .GetString()
            .Should()
            .Be("flex");
        root.GetProperty("provider")
            .GetProperty("allow_fallbacks")
            .GetBoolean()
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task ReadAsByteArrayAsync_WithAttachment_UsesOpenRouterImageReferenceSchema()
    {
        byte[] attachmentBytes = [1, 2, 3, 4];
        TestAttachmentSource attachment = new(attachmentBytes);
        StreamingGenerationProviderContext context = CreateContext(attachment);
        using OpenRouterImageRequestContent content = new(context);

        byte[] serialized = await content.ReadAsByteArrayAsync();

        serialized.LongLength.Should().Be(content.Headers.ContentLength);
        using JsonDocument document = JsonDocument.Parse(serialized);
        JsonElement reference = document.RootElement.GetProperty("input_references")[0];
        reference.GetProperty("type").GetString().Should().Be("image_url");
        string? dataUrl = reference.GetProperty("image_url").GetProperty("url").GetString();
        dataUrl.Should().Be($"data:image/png;base64,{Convert.ToBase64String(attachmentBytes)}");
    }

    private static StreamingGenerationProviderContext CreateContext(
        string modelId,
        string providerModelId,
        string resolution,
        string aspectRatio,
        string? quality = null)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadCatalog()
            .Models.Single(model => model.Id == modelId);
        StreamingImageGenerationRequest request = new(
            Guid.Parse("b06c4d8c-2d05-4fce-a005-2ec1e3da9b82"),
            1,
            modelId,
            "A bright landscape",
            aspectRatio,
            resolution,
            1d,
            null,
            CreateParameters(quality),
            []);

        return new StreamingGenerationProviderContext(
            request,
            GenerationProviderIds.OpenRouter,
            providerModelId,
            metadata.Pricing,
            TestGenerationCredentials.ProviderCredential,
            metadata.TransportLimits);
    }

    private static IReadOnlyDictionary<string, JsonElement> CreateParameters(string? quality)
    {
        Dictionary<string, JsonElement> parameters = new(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(quality))
        {
            parameters[GenerationParameterNames.Quality] =
                JsonSerializer.SerializeToElement(quality);
        }

        return parameters;
    }

    private static StreamingGenerationProviderContext CreateContext(
        params IGenerationAttachmentSource[] attachments)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadCatalog()
            .Models.Single(model => model.Id == "openrouter-gpt-image-2");
        StreamingImageGenerationRequest request = new(
            Guid.Parse("b06c4d8c-2d05-4fce-a005-2ec1e3da9b82"),
            1,
            metadata.Id,
            "A bright landscape",
            "auto",
            "1K",
            1d,
            null,
            new Dictionary<string, JsonElement>(),
            attachments);

        return new StreamingGenerationProviderContext(
            request,
            GenerationProviderIds.OpenRouter,
            metadata.ProviderModelId,
            metadata.Pricing,
            TestGenerationCredentials.ProviderCredential,
            metadata.TransportLimits);
    }

    private sealed class TestAttachmentSource : IGenerationAttachmentSource
    {
        private readonly byte[] _content;

        public GenerationAttachmentMetadataDto Metadata { get; }

        public TestAttachmentSource(
            byte[] content,
            int pixelWidth = 0,
            int pixelHeight = 0)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            Metadata = new GenerationAttachmentMetadataDto(
                0,
                "reference.png",
                GenerationImageContentTypes.Png,
                content.LongLength,
                pixelWidth,
                pixelHeight);
        }

        public ValueTask<Stream> OpenReadAsync(CancellationToken ct)
        {
            return ValueTask.FromResult<Stream>(new MemoryStream(_content, writable: false));
        }
    }
}
