using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ImageGenerationApiClientTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

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
        HttpClient httpClient)
    {
        Mock<IGenerationStreamingResultStore> resultStore = new();
        ProviderResponseImageDecoderRegistry decoderRegistry = new(
            Array.Empty<IProviderResponseImageDecoder>());

        return new ImageGenerationApiClient(
            httpClient,
            TestApiEndpointServiceFactory.Create(),
            resultStore.Object,
            decoderRegistry,
            NullLogger<ImageGenerationApiClient>.Instance,
            TestApiConfiguration.CreateApiClientOptionsWrapper(),
            TestApiConfiguration.CreateGenerationOptionsWrapper(),
            new SkiaAttachedImageCodec(
                TestApiConfiguration.CreateGenerationOptionsWrapper()));
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
