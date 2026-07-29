using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using FluentAssertions;
using Xunit;

using AtomicArt.Api.Generation;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.Generation;

public sealed class MultipartGenerationRequestReaderTests
{
    private const int MaximumMetadataBytes = 1_048_576;

    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

    [Theory]
    [InlineData('Я')]
    [InlineData('界')]
    [InlineData('\0')]
    public async Task ReadAsync_WithMaximumLengthPrompt_ReturnsMetadata(
        char promptCharacter)
    {
        GenerationModelMetadataDto model =
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        string prompt = new(promptCharacter, model.MaxPromptLength);
        using MultipartFormDataContent requestContent =
            CreateRequestContent(model.Id, prompt);
        await using Stream requestBody =
            await requestContent.ReadAsStreamAsync();
        HttpRequest request = CreateHttpRequest(requestContent, requestBody);
        MultipartGenerationRequestReader reader =
            CreateReader(MaximumMetadataBytes);

        await using MultipartGenerationRequest result =
            await reader.ReadAsync(request, CancellationToken.None);

        result.Metadata.Prompt.Should().Be(prompt);
    }

    [Fact]
    public async Task ReadAsync_WhenMetadataExceedsLimit_PreservesSizeLimitException()
    {
        using MultipartFormDataContent requestContent =
            CreateRequestContent(
                ApiModelMetadataTestCatalog.NanoBanana2ModelId,
                "Prompt");
        await using Stream requestBody =
            await requestContent.ReadAsStreamAsync();
        HttpRequest request = CreateHttpRequest(requestContent, requestBody);
        MultipartGenerationRequestReader reader = CreateReader(64);

        Func<Task> act = () => reader.ReadAsync(
            request,
            CancellationToken.None);

        GenerationMultipartRequestException exception = (await act
                .Should()
                .ThrowAsync<GenerationMultipartRequestException>())
            .Which;
        exception.Message.Should().Be(
            "The multipart request part exceeds the allowed size.");
        exception.InnerException.Should().BeNull();
    }

    private static MultipartGenerationRequestReader CreateReader(
        int maxMetadataBytes)
    {
        GenerationServerOptions options = new()
        {
            CopyBufferSize = 4096,
            EmergencyMaxProviderResponseBytes = 1024L * 1024L,
            MaximumBoundaryLength = 256,
            MaxConcurrentGenerations = 1,
            MaxMetadataBytes = maxMetadataBytes,
            MaxRequestBytes = 2L * 1024L * 1024L
        };

        return new MultipartGenerationRequestReader(Options.Create(options));
    }

    private static HttpRequest CreateHttpRequest(
        MultipartFormDataContent content,
        Stream body)
    {
        DefaultHttpContext context = new();
        context.Request.ContentType = content.Headers.ContentType?.ToString();
        context.Request.ContentLength = content.Headers.ContentLength;
        context.Request.Body = body;

        return context.Request;
    }

    private static MultipartFormDataContent CreateRequestContent(
        string modelId,
        string prompt)
    {
        GenerationRequestMetadataDto metadata = new(
            LogicalGenerationId,
            1,
            modelId,
            prompt,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Array.Empty<GenerationAttachmentMetadataDto>());
        string metadataJson = JsonSerializer.Serialize(
            metadata,
            SerializerOptions);
        MultipartFormDataContent content = new();
        content.Add(
            new StringContent(
                metadataJson,
                Encoding.UTF8,
                "application/json"),
            GenerationApiRoutes.MetadataPartName);

        return content;
    }
}
