using System.Text;
using System.Text.Json;

using FluentAssertions;
using Xunit;

using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation.OpenRouter;

namespace AtomicArt.Infrastructure.Tests.Generation.OpenRouter;

public sealed class OpenRouterImageResponseMetadataReaderTests
{
    private const int MaximumMetadataBytes = 1024;
    private const int MaximumStructureDepth = 8;
    private const int LargeBase64Length = 100000;

    [Theory]
    [InlineData(GenerationImageContentTypes.Jpeg, true, 1)]
    [InlineData(GenerationImageContentTypes.Jpeg, false, 127)]
    [InlineData(GenerationImageContentTypes.Webp, true, 127)]
    [InlineData(GenerationImageContentTypes.Webp, false, 1)]
    public void ReadContentType_WithLargeImageAndSplitMetadata_ReturnsDeclaredFormat(
        string contentType,
        bool mediaTypeFirst,
        int chunkSize)
    {
        string base64 = new string('A', LargeBase64Length) + "\\u003D\\u003D";
        string mediaTypeProperty = $"\"media_type\":{JsonSerializer.Serialize(contentType)}";
        string imageProperty = $"\"b64_json\":\"{base64}\"";
        string properties = mediaTypeFirst
            ? $"{mediaTypeProperty},{imageProperty}"
            : $"{imageProperty},{mediaTypeProperty}";
        byte[] responseBytes = Encoding.UTF8.GetBytes($"{{\"data\":[{{{properties}}}]}}");
        using OpenRouterImageResponseMetadataReader reader = new(MaximumMetadataBytes, MaximumStructureDepth);

        for (int offset = 0; offset < responseBytes.Length; offset += chunkSize)
        {
            reader.Append(responseBytes.AsSpan(offset, Math.Min(chunkSize, responseBytes.Length - offset)));
        }

        reader.ReadContentType().Should().Be(contentType);
    }

    [Fact]
    public void ReadContentType_WithoutMediaType_PreservesPngDefault()
    {
        using OpenRouterImageResponseMetadataReader reader = new(MaximumMetadataBytes, MaximumStructureDepth);
        reader.Append("{\"data\":[{\"b64_json\":\"AA==\"}]}"u8);

        string contentType = reader.ReadContentType();

        contentType.Should().Be(GenerationImageContentTypes.Png);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":[{},{}]}")]
    [InlineData("{\"data\":[{\"b64_json\":\"AA==\",\"media_type\":\"image/svg+xml\"}]}")]
    [InlineData("{\"data\":[{\"b64_json\":\"AA==\",\"media_type\":null}]}")]
    [InlineData("{\"data\":[{\"b64_json\":\"AA==")]
    public void ReadContentType_WithInvalidResponse_ThrowsProviderInvalidResponse(string response)
    {
        using OpenRouterImageResponseMetadataReader reader = new(MaximumMetadataBytes, MaximumStructureDepth);
        reader.Append(Encoding.UTF8.GetBytes(response));

        Func<string> act = reader.ReadContentType;

        act.Should().Throw<OpenRouterImageException>().Which.FailureKind
            .Should().Be(ImageGenerationProviderFailureKind.InvalidResponse);
    }

    [Fact]
    public void Append_WithOversizedMetadata_ThrowsProviderInvalidResponse()
    {
        using OpenRouterImageResponseMetadataReader reader = new(MaximumMetadataBytes, MaximumStructureDepth);
        byte[] response = Encoding.UTF8.GetBytes($"{{\"detail\":\"{new string('A', MaximumMetadataBytes)}\"}}");

        Action act = () => reader.Append(response);

        act.Should().Throw<OpenRouterImageException>().Which.FailureKind
            .Should().Be(ImageGenerationProviderFailureKind.InvalidResponse);
    }
}
