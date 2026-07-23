using System.Net;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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
            NullLogger<ImageGenerationApiClient>.Instance);
    }

    private static ImageGenerationRequestDto CreateRequest()
    {
        List<AttachedImageDto> attachedImages =
        [
            new AttachedImageDto(
                "reference.png",
                GenerationImageContentTypes.Png,
                new byte[]
                {
                    0x89,
                    0x50,
                    0x4E,
                    0x47
                })
        ];

        return ImageGenerationRequestDtoTestFactory.Create(
            prompt: "Create a studio product shot",
            aspectRatio: "16:9",
            attachedImages: attachedImages);
    }
}
