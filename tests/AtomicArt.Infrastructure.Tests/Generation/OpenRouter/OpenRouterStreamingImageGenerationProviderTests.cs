using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.OpenRouter;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

public sealed class OpenRouterStreamingImageGenerationProviderTests
{
    private const long MaximumBytes = 1024 * 1024;
    private const int ResponseBufferSize = 128;

    private static readonly string ImageResponseJson = JsonSerializer.Serialize(new
    {
        data = new[]
        {
            new { b64_json = Convert.ToBase64String(GenerationImageTestData.ValidPngBytes) }
        }
    });

    [Theory]
    [InlineData(GenerationImageContentTypes.Jpeg)]
    [InlineData(GenerationImageContentTypes.Webp)]
    public async Task CreateStreamAsync_WithImageApiMediaType_PreservesDeclaredFormatAndPrice(string contentType)
    {
        StreamingGenerationProviderContext context = OpenRouterGenerationContextTestFactory.Create(
            ApiModelMetadataTestCatalog.OpenRouterNanoBanana21ModelId,
            "4K");
        string responseJson = JsonSerializer.Serialize(new
        {
            data = new[] { new { media_type = contentType, b64_json = "AQIDBA==" } },
            usage = new { cost = 0.0123m }
        });
        using ProviderTestContext testContext = new(responseJson);

        await using IProviderGenerationStream stream = await testContext.Provider.CreateStreamAsync(
            context, CancellationToken.None);
        using MemoryStream destination = new();
        await stream.CopyToAsync(destination, MaximumBytes, CancellationToken.None);

        stream.Summary?.ContentTypes.Should().Equal(contentType);
        stream.Summary?.Price.Should().BeEquivalentTo(new GenerationPriceDto(
            0.0123m, "USD", GenerationPriceSources.ActualProviderUsage));
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(responseJson);
    }

    [Fact]
    public async Task CreateStreamAsync_WithNanoBanana21FourK_UsesImageApiAndPreservesResponse()
    {
        StreamingGenerationProviderContext context = OpenRouterGenerationContextTestFactory.Create(
            ApiModelMetadataTestCatalog.OpenRouterNanoBanana21ModelId,
            "4K",
            "16:9");
        using ProviderTestContext testContext = new();

        await using IProviderGenerationStream stream = await testContext.Provider.CreateStreamAsync(
            context, CancellationToken.None);
        using MemoryStream destination = new();
        await stream.CopyToAsync(destination, MaximumBytes, CancellationToken.None);

        testContext.RequestPath.Should().Be("/api/v1/images");
        using JsonDocument request = JsonDocument.Parse(testContext.RequestBody);
        request.RootElement.GetProperty("resolution").GetString().Should().Be("4K");
        request.RootElement.GetProperty("aspect_ratio").GetString().Should().Be("16:9");
        request.RootElement.TryGetProperty("image_config", out _).Should().BeFalse();
        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(ImageResponseJson);
    }

    [Theory]
    [InlineData("openrouter-nano-banana-2", "/api/v1/chat/completions")]
    [InlineData("openrouter-nano-banana-2-lite", "/api/v1/chat/completions")]
    [InlineData("openrouter-nano-banana-pro", "/api/v1/chat/completions")]
    [InlineData("openrouter-gpt-image-2", "/api/v1/images")]
    [InlineData("openrouter-gpt-image-2-5-sunburst", "/api/v1/images")]
    [InlineData("openrouter-gpt-image-2-5-flare", "/api/v1/images")]
    public async Task CreateStreamAsync_WithExistingModel_PreservesApiRoute(
        string modelId,
        string expectedPath)
    {
        StreamingGenerationProviderContext context = OpenRouterGenerationContextTestFactory.Create(modelId);
        using ProviderTestContext testContext = new();

        await using IProviderGenerationStream stream = await testContext.Provider.CreateStreamAsync(
            context, CancellationToken.None);

        testContext.RequestPath.Should().Be(expectedPath);
    }

    private sealed class ProviderTestContext : IDisposable
    {
        public OpenRouterStreamingImageGenerationProvider Provider { get; }
        public string? RequestPath { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        private readonly HttpClient _httpClient;

        public ProviderTestContext(string? responseJson = null)
        {
            Mock<HttpMessageHandler> handler = new();
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken ct) =>
                {
                    RequestPath = request.RequestUri?.AbsolutePath;
                    HttpContent content = request.Content
                        ?? throw new InvalidOperationException("The provider request content is required.");
                    RequestBody = await content.ReadAsStringAsync(ct).ConfigureAwait(false);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson ?? ImageResponseJson, Encoding.UTF8, "application/json")
                    };
                });
            _httpClient = new HttpClient(handler.Object)
            {
                BaseAddress = new Uri("https://openrouter.ai")
            };
            IOptions<OpenRouterImageOptions> options = Options.Create(new OpenRouterImageOptions
            {
                ChatCompletionsPath = "/api/v1/chat/completions",
                ImagesPath = "/api/v1/images",
                MaxRequestBytes = MaximumBytes,
                MaxResponseBytes = MaximumBytes,
                ResponseBufferSize = ResponseBufferSize
            });
            OpenRouterImageClient client = new(
                _httpClient,
                NullLogger<OpenRouterImageClient>.Instance,
                new OpenRouterImageFailureClassifier(),
                options);
            Provider = new OpenRouterStreamingImageGenerationProvider(client, options);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
