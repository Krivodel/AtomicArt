using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.OpenRouter;
using AtomicArt.Infrastructure.Generation.GoogleInteractions;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

public sealed class OpenRouterChatCompletionRequestContentTests
{
    [Fact]
    public async Task ReadAsStringAsync_WithFourKAndFixedAspectRatio_SendsImageConfiguration()
    {
        StreamingGenerationProviderContext context = CreateContext("4K", "16:9", "high");
        using OpenRouterChatCompletionRequestContent content = new(
            context,
            OpenRouterFlexModelPolicy.GoogleAiStudioFlexProviderTag);

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());
        JsonElement root = document.RootElement;

        root.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("system");
        root.GetProperty("messages")[0].GetProperty("content").GetString()
            .Should().Be(GoogleInteractionsImageOutputContract.SystemInstruction);

        root.GetProperty("image_config").GetProperty("image_size").GetString().Should().Be("4K");
        root.GetProperty("image_config").GetProperty("aspect_ratio").GetString().Should().Be("16:9");
        root.GetProperty("temperature").GetDouble().Should().Be(1d);
        root.GetProperty("reasoning_effort").GetString().Should().Be("high");
        root.GetProperty("service_tier").GetString().Should().Be("flex");
        root.GetProperty("provider").GetProperty("only")[0].GetString()
            .Should().Be(OpenRouterFlexModelPolicy.GoogleAiStudioFlexProviderTag);
        root.GetProperty("provider").GetProperty("allow_fallbacks").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsStringAsync_WithAutomaticAspectRatio_OmitsAspectRatioFromImageConfiguration()
    {
        StreamingGenerationProviderContext context = CreateContext("2K", "auto", null);
        using OpenRouterChatCompletionRequestContent content = new(
            context,
            null);

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());
        JsonElement imageConfiguration = document.RootElement.GetProperty("image_config");

        imageConfiguration.GetProperty("image_size").GetString().Should().Be("2K");
        imageConfiguration.TryGetProperty("aspect_ratio", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("reasoning_effort", out _).Should().BeFalse();
        document.RootElement.GetProperty("service_tier").GetString().Should().Be("flex");
        document.RootElement.TryGetProperty("provider", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsStringAsync_WithAttachment_UsesOpenRouterChatReferenceSchema()
    {
        byte[] attachmentBytes = [1, 2, 3, 4];
        TestAttachmentSource attachment = new(attachmentBytes);
        StreamingGenerationProviderContext context = CreateContext(
            "1K",
            "auto",
            null,
            [attachment]);
        using OpenRouterChatCompletionRequestContent content = new(
            context,
            OpenRouterFlexModelPolicy.GoogleAiStudioFlexProviderTag);

        using JsonDocument document = JsonDocument.Parse(await content.ReadAsStringAsync());
        JsonElement reference = document.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")[1];

        reference.GetProperty("type").GetString().Should().Be("image_url");
        reference.GetProperty("image_url").GetProperty("url").GetString()
            .Should().Be($"data:image/png;base64,{Convert.ToBase64String(attachmentBytes)}");
    }

    private static StreamingGenerationProviderContext CreateContext(
        string resolution,
        string aspectRatio,
        string? thinkingLevel,
        IReadOnlyList<IGenerationAttachmentSource>? attachments = null)
    {
        GenerationModelMetadataDto metadata = ApiModelMetadataTestCatalog.LoadCatalog()
            .Models.Single(model => model.Id == "openrouter-nano-banana-pro");
        StreamingImageGenerationRequest request = new(
            Guid.Parse("b06c4d8c-2d05-4fce-a005-2ec1e3da9b82"),
            1,
            metadata.Id,
            "A bright landscape",
            aspectRatio,
            resolution,
            1d,
            thinkingLevel,
            new Dictionary<string, JsonElement>(),
            attachments ?? []);

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

        public TestAttachmentSource(byte[] content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            Metadata = new GenerationAttachmentMetadataDto(
                0,
                "reference.png",
                GenerationImageContentTypes.Png,
                content.LongLength);
        }

        public ValueTask<Stream> OpenReadAsync(CancellationToken ct)
        {
            return ValueTask.FromResult<Stream>(new MemoryStream(_content, writable: false));
        }
    }
}
