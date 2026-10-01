using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using SkiaSharp;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ImageGenerationApiClientTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task CreateGenerationAsync_WithEscapedOpenRouterImage_SavesOriginalImage()
    {
        const int TextLength = 1024;
        using TemporaryDirectory directory = new(
            typeof(ImageGenerationApiClientTests),
            nameof(CreateGenerationAsync_WithEscapedOpenRouterImage_SavesOriginalImage));
        AtomicArtDataPathProvider pathProvider = new(directory.DirectoryPath);
        GenerationStreamingResultStore resultStore = new(
            pathProvider,
            GenerationImageFormatRegistryTestFactory.Create(),
            new GenerationImageFileNamePolicy(),
            TestApiConfiguration.CreateTrustedFileStreamFactory());
        byte[] imageBytes = GenerationImageTestData.ValidPngBytes;
        string escapedBase64 = Convert.ToBase64String(imageBytes)
            .Replace("/", "\\/", StringComparison.Ordinal)
            .Replace("+", "\\u002B", StringComparison.Ordinal)
            .Replace("=", "\\u003D", StringComparison.Ordinal);
        string providerResponseJson = $$"""
        {
          "choices": [{ "message": {
            "content": "{{new string('A', TextLength)}}",
            "images": [{ "image_url": { "url": "data:image/png;base64,{{escapedBase64}}" } }]
          } }]
        }
        """;
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateSuccessfulStreamingResponse(providerResponseJson));
        using HttpClient httpClient = new(handler.Object);
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient, resultStore, decoder);

        GenerationBatchDto batch = await apiClient.CreateGenerationAsync(
            CreateRequest(attachedImageContent: imageBytes),
            LogicalGenerationId,
            1,
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        GenerationItemDto item = batch.Items.Should().ContainSingle().Which;
        item.Status.Should().Be(GenerationItemStatus.Generated);
        string imagePath = item.ImagePath
            ?? throw new InvalidOperationException("Generated image path is missing.");
        byte[] savedBytes = await File.ReadAllBytesAsync(imagePath);
        savedBytes.Should().Equal(imageBytes);
        Directory.GetFiles(pathProvider.ArtDirectory).Should().Equal(imagePath);
    }

    [Fact]
    public async Task CreateGenerationAsync_WithRetryableProblemDetails_ThrowsRetryableAttemptException()
    {
        string problemDetails = $$"""
        {
          "status": 503,
          "code": "{{GenerationProviderFailureErrorCodes.Unavailable}}",
          "retryable": true
        }
        """;
        CapturingHttpMessageHandler handler = new(
            problemDetails,
            HttpStatusCode.ServiceUnavailable);
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient);

        Func<Task> act = () => apiClient.CreateGenerationAsync(
            CreateRequest(),
            LogicalGenerationId,
            1,
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        GenerationAttemptException exception = (await act
                .Should()
                .ThrowAsync<GenerationAttemptException>())
            .Which;
        exception.Retryable.Should().BeTrue();
        exception.SafeErrorCode.Should().Be(
            GenerationProviderFailureErrorCodes.Unavailable);
    }

    [Fact]
    public async Task CreateGenerationAsync_WithProtocolProblemDetails_PreservesSafeErrorCode()
    {
        string problemDetails = $$"""
        {
          "status": 400,
          "code": "{{GenerationProtocolErrorCodes.ModelNotFound}}",
          "retryable": false
        }
        """;
        CapturingHttpMessageHandler handler = new(
            problemDetails,
            HttpStatusCode.BadRequest);
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient);

        Func<Task> act = () => apiClient.CreateGenerationAsync(
            CreateRequest(),
            LogicalGenerationId,
            1,
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        GenerationAttemptException exception = (await act
                .Should()
                .ThrowAsync<GenerationAttemptException>())
            .Which;
        exception.SafeErrorCode.Should().Be(
            GenerationProtocolErrorCodes.ModelNotFound);
    }

    [Fact]
    public async Task CreateGenerationAsync_WithAttachment_SendsVersionTwoMultipartRequest()
    {
        string problemDetails = """
        {
          "status": 400,
          "code": "GENERATION_INVALID_MULTIPART_REQUEST",
          "retryable": false
        }
        """;
        CapturingHttpMessageHandler handler = new(
            problemDetails,
            HttpStatusCode.BadRequest);
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient);

        Func<Task> act = () => apiClient.CreateGenerationAsync(
            CreateRequest(),
            LogicalGenerationId,
            3,
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        await act.Should().ThrowAsync<GenerationAttemptException>();
        handler.RequestMethod.Should().Be(HttpMethod.Post);
        handler.RequestUri?.AbsolutePath.Should().Be(
            $"/{GenerationApiRoutes.Generations}");
        handler.RequestBody.Should().Contain(
            $"\"logicalGenerationId\":\"{LogicalGenerationId}\"");
        handler.RequestBody.Should().Contain("\"attemptNumber\":3");
        handler.RequestBody.Should().Contain("name=metadata");
        handler.RequestBody.Should().Contain("name=attachment-0");
        handler.ProviderCredential.Should().Be(
            TestGenerationCredentials.ProviderCredential);
    }

    [Fact]
    public async Task CreateGenerationAsync_WithUnicodePrompt_SendsUnescapedPrompt()
    {
        const string Prompt = "Нарисуй уютный дом у моря — 東京";
        string problemDetails = """
        {
          "status": 400,
          "code": "GENERATION_INVALID_MULTIPART_REQUEST",
          "retryable": false
        }
        """;
        CapturingHttpMessageHandler handler = new(
            problemDetails,
            HttpStatusCode.BadRequest);
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient);

        Func<Task> act = () => apiClient.CreateGenerationAsync(
            CreateRequest(Prompt),
            LogicalGenerationId,
            1,
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        await act.Should().ThrowAsync<GenerationAttemptException>();
        handler.RequestBody.Should().Contain($"\"prompt\":\"{Prompt}\"");
        handler.RequestBody.Should().NotContain("\\u041d");
        handler.RequestBody.Should().NotContain("\\u6771");
    }

    [Fact]
    public async Task CreateGenerationAsync_WithValidAttachment_SendsPixelDimensionsInMetadata()
    {
        string problemDetails = """
        {
          "status": 400,
          "code": "GENERATION_INVALID_MULTIPART_REQUEST",
          "retryable": false
        }
        """;
        CapturingHttpMessageHandler handler = new(
            problemDetails,
            HttpStatusCode.BadRequest);
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient);

        Func<Task> act = () => apiClient.CreateGenerationAsync(
            CreateRequest(attachedImageContent: CreateEncodedImage(1600, 800)),
            LogicalGenerationId,
            1,
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        await act.Should().ThrowAsync<GenerationAttemptException>();
        handler.RequestBody.Should().Contain("\"pixelWidth\":1600");
        handler.RequestBody.Should().Contain("\"pixelHeight\":800");
    }

    private static ImageGenerationApiClient CreateApiClient(
        HttpClient httpClient,
        IGenerationStreamingResultStore? resultStore = null,
        IProviderResponseImageDecoder? decoder = null)
    {
        IGenerationStreamingResultStore streamingResultStore = resultStore
            ?? new Mock<IGenerationStreamingResultStore>().Object;
        IProviderResponseImageDecoder[] decoders = decoder is null
            ? Array.Empty<IProviderResponseImageDecoder>()
            : new IProviderResponseImageDecoder[] { decoder };
        ProviderResponseImageDecoderRegistry decoderRegistry = new(
            decoders);

        return new ImageGenerationApiClient(
            httpClient,
            TestApiEndpointServiceFactory.Create(),
            streamingResultStore,
            decoderRegistry,
            NullLogger<ImageGenerationApiClient>.Instance,
            TestApiConfiguration.CreateApiClientOptionsWrapper(),
            TestApiConfiguration.CreateGenerationOptionsWrapper(),
            new SkiaAttachedImageCodec(
                TestApiConfiguration.CreateGenerationOptionsWrapper()));
    }

    private static HttpResponseMessage CreateSuccessfulStreamingResponse(string providerResponseJson)
    {
        MultipartContent content = new("mixed", "test-generation-response");
        StringContent providerContent = new(providerResponseJson, Encoding.UTF8, "application/json");
        providerContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            Name = GenerationApiRoutes.ProviderResponsePartName
        };
        providerContent.Headers.Add("X-AtomicArt-Provider-Id", GenerationProviderIds.OpenRouter);
        content.Add(providerContent);
        GenerationAttemptMetadataDto metadata = new(
            LogicalGenerationId,
            1,
            GenerationProviderIds.OpenRouter,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Test image model",
            GenerationItemStatus.Generated,
            "completed",
            1,
            new string[] { GenerationImageContentTypes.Png },
            null,
            null,
            new DateTime(2026, 10, 1, 6, 53, 0, DateTimeKind.Utc),
            TimeSpan.FromSeconds(30),
            null,
            false);
        StringContent metadataContent = new(
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8,
            "application/json");
        metadataContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            Name = GenerationApiRoutes.GenerationMetadataPartName
        };
        content.Add(metadataContent);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        };
    }

    private static ImageGenerationRequestDto CreateRequest(
        string prompt = "Create a studio product shot",
        byte[]? attachedImageContent = null)
    {
        List<AttachedImageDto> attachedImages =
        [
            new AttachedImageDto(
                "reference.png",
                GenerationImageContentTypes.Png,
                attachedImageContent ??
                new byte[]
                {
                    0x89,
                    0x50,
                    0x4E,
                    0x47
                })
        ];

        return ImageGenerationRequestDtoTestFactory.Create(
            prompt: prompt,
            aspectRatio: "16:9",
            attachedImages: attachedImages);
    }

    private static byte[] CreateEncodedImage(int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.White);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
