using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;
using TestGenerationCredentials = AtomicArt.Tests.Common.Generation.TestGenerationCredentials;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class ImageGenerationApiClientTests
{
    private const string ImageBase64Data = "AQIDBA==";
    private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime CreatedAtUtc = new(2026, 7, 2, 10, 30, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateAsync_WithImageContent_DeserializesBatch()
    {
        ImageGenerationRequestDto request = CreateRequest();
        CapturingHttpMessageHandler handler = new(CreateBatchJson());
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient);

        GenerationBatchDto batch = await apiClient.CreateGenerationAsync(
            request,
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        handler.RequestMethod.Should().Be(HttpMethod.Post);
        Uri requestUri = handler.RequestUri
            ?? throw new InvalidOperationException("Request URI must be captured.");
        requestUri.AbsolutePath.Should().Be($"/{GenerationApiRoutes.Generations}");
        ImageGenerationRequestDto? deserializedRequest = JsonSerializer.Deserialize<ImageGenerationRequestDto>(
            handler.RequestBody,
            JsonOptions);
        deserializedRequest.Should().BeEquivalentTo(request);
        handler.ProviderCredential.Should().Be(TestGenerationCredentials.ProviderCredential);
        batch.BatchId.Should().Be(BatchId);
        GenerationItemDto item = batch.Items.Should().ContainSingle().Which;
        item.Id.Should().Be(ItemId);
        item.ImagePath.Should().BeNull();
        GenerationImageContentDto imageContent = item.ImageContent
            ?? throw new InvalidOperationException("Image content must be deserialized.");
        imageContent.ContentType.Should().Be("image/png");
        imageContent.Base64Data.Should().Be(ImageBase64Data);
    }

    [Theory]
    [InlineData("https://atomicart.test/")]
    [InlineData("http://localhost/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://atomicart.test/")]
    public async Task CreateAsync_WithTrustedProviderCredentialTarget_SendsProviderCredential(string baseAddress)
    {
        CapturingHttpMessageHandler handler = new(CreateBatchJson());
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(
            httpClient,
            TestApiEndpointServiceFactory.Create(baseAddress));

        await apiClient.CreateGenerationAsync(
            CreateRequest(),
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        handler.ProviderCredential.Should().Be(TestGenerationCredentials.ProviderCredential);
    }

    [Fact]
    public async Task CreateAsync_WithoutProviderCredential_DoesNotSendProviderCredentialHeader()
    {
        CapturingHttpMessageHandler handler = new(CreateBatchJson());
        using HttpClient httpClient = new(handler);
        ImageGenerationApiClient apiClient = CreateApiClient(
            httpClient,
            TestApiEndpointServiceFactory.Create("http://atomicart.test/"));

        await apiClient.CreateGenerationAsync(
            CreateRequest(),
            string.Empty,
            CancellationToken.None);

        handler.RequestMethod.Should().Be(HttpMethod.Post);
        handler.ProviderCredential.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_AfterEndpointChanges_UsesNewAddress()
    {
        CapturingHttpMessageHandler handler = new(CreateBatchJson());
        using HttpClient httpClient = new(handler);
        IApiEndpointService endpointService = TestApiEndpointServiceFactory.Create(
            "https://first.atomicart.test/");
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient, endpointService);

        await apiClient.CreateGenerationAsync(
            CreateRequest(),
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);
        ApiBaseAddress.TryCreate(
            "https://second.atomicart.test/root/",
            out ApiBaseAddress? secondAddress).Should().BeTrue();
        endpointService.SetBaseAddress(secondAddress
            ?? throw new InvalidOperationException("Second address is required."));

        await apiClient.CreateGenerationAsync(
            CreateRequest(),
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        handler.RequestUris.Should().Equal(
            new Uri($"https://first.atomicart.test/{GenerationApiRoutes.Generations}"),
            new Uri($"https://second.atomicart.test/root/{GenerationApiRoutes.Generations}"));
    }

    [Fact]
    public async Task CreateAsync_WithProblemDetails_LogsOnlySafeStatusAndErrorCode()
    {
        const string ConfidentialDetail =
            "Prompt and provider-key-confidential must never be logged.";
        const string ErrorCode = GenerationProviderFailureErrorCodes.Unavailable;
        string problemDetails = $$"""
        {
          "status": 503,
          "title": "Provider unavailable",
          "detail": "{{ConfidentialDetail}}",
          "code": "{{ErrorCode}}"
        }
        """;
        CapturingHttpMessageHandler handler = new(
            problemDetails,
            HttpStatusCode.ServiceUnavailable);
        using HttpClient httpClient = new(handler);
        RecordingLogger<ImageGenerationApiClient> logger = new();
        ImageGenerationApiClient apiClient = CreateApiClient(httpClient, logger: logger);

        Func<Task> act = () => apiClient.CreateGenerationAsync(
            CreateRequest(),
            TestGenerationCredentials.ProviderCredential,
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        logger.Messages.Should().Contain(message =>
            message.Contains("503", StringComparison.Ordinal)
            && message.Contains(ErrorCode, StringComparison.Ordinal));
        logger.Messages.Should().OnlyContain(message =>
            !message.Contains(ConfidentialDetail, StringComparison.Ordinal)
            && !message.Contains(TestGenerationCredentials.ProviderCredential, StringComparison.Ordinal)
            && !message.Contains("Create a studio product shot", StringComparison.Ordinal));
    }


    private static ImageGenerationApiClient CreateApiClient(
        HttpClient httpClient,
        IApiEndpointService? endpointService = null,
        ILogger<ImageGenerationApiClient>? logger = null)
    {
        return new ImageGenerationApiClient(
            httpClient,
            endpointService ?? TestApiEndpointServiceFactory.Create(),
            logger ?? NullLogger<ImageGenerationApiClient>.Instance);
    }

    private static ImageGenerationRequestDto CreateRequest()
    {
        List<AttachedImageDto> attachedImages =
        [
            new AttachedImageDto(
                "reference.png",
                "image/png",
                new byte[] { 0x01, 0x02, 0x03 })
        ];

        return ImageGenerationRequestDtoTestFactory.Create(
            prompt: "Create a studio product shot",
            aspectRatio: "16:9",
            attachedImages: attachedImages);
    }

    private static string CreateBatchJson()
    {
        return $$"""
        {
          "batchId": "{{BatchId}}",
          "items": [
            {
              "id": "{{ItemId}}",
              "modelId": "{{ApiModelMetadataTestCatalog.NanoBanana2ModelId}}",
              "modelDisplayName": "{{ApiModelMetadataTestCatalog.NanoBanana2DisplayName}}",
              "prompt": "Create a studio product shot",
              "aspectRatio": "16:9",
              "resolution": "{{TestGenerationOutputMetadata.GeneratedImageResolution}}",
              "createdAtUtc": "{{CreatedAtUtc:O}}",
              "status": "Generated",
              "imagePath": null,
              "imageContent": {
                "contentType": "image/png",
                "base64Data": "{{ImageBase64Data}}"
              }
            }
          ]
        }
        """;
    }
}
