using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class JsonBase64ProviderResponseImageDecoderTests
{
    [Fact]
    public async Task DecodeAsync_WithChunkedBase64_WritesImageDirectlyToDestination()
    {
        byte[] imageBytes = Enumerable
            .Range(0, 100003)
            .Select(value => (byte)(value % 251))
            .ToArray();
        string responseJson = $$"""
        {
          "status": "completed",
          "output": [
            {
              "type": "image",
              "mime_type": "image/png",
              "data": "{{Convert.ToBase64String(imageBytes)}}"
            }
          ]
        }
        """;
        await using Stream input = new ChunkedMemoryStream(
            Encoding.UTF8.GetBytes(responseJson),
            maximumReadSize: 19);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());

        ProviderResponseImageDecodeResult result = new();
        await decoder.DecodeAsync(
            input,
            output,
            result,
            CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(imageBytes);
    }

    [Fact]
    public async Task DecodeAsync_WithoutImage_AllowsTrailingMetadataToClassifyFailure()
    {
        string responseJson = """
        {
          "status": "failed",
          "error": {
            "status": "INTERNAL"
          }
        }
        """;
        await using Stream input = new MemoryStream(
            Encoding.UTF8.GetBytes(responseJson),
            writable: false);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());

        ProviderResponseImageDecodeResult result = new();
        await decoder.DecodeAsync(
            input,
            output,
            result,
            CancellationToken.None);

        result.HasImage.Should().BeFalse();
        output.Length.Should().Be(0);
    }

    [Fact]
    public async Task DecodeAsync_WithOpenRouterBase64Json_WritesImageDirectlyToDestination()
    {
        byte[] imageBytes = [1, 2, 3, 4];
        string responseJson = $$"""{ "data": [{ "b64_json": "{{Convert.ToBase64String(imageBytes)}}" }] }""";
        await using Stream input = new MemoryStream(Encoding.UTF8.GetBytes(responseJson), writable: false);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());

        decoder.CanDecode(GenerationProviderIds.OpenRouter, "application/json").Should().BeTrue();

        ProviderResponseImageDecodeResult result = new();
        await decoder.DecodeAsync(input, output, result, CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(imageBytes);
    }

    [Fact]
    public async Task DecodeAsync_WithOpenRouterImageAndNonImageUrl_DecodesOnlyImage()
    {
        byte[] imageBytes = [1, 2, 3, 4];
        string responseJson = $$"""
        {
          "choices": [
            {
              "message": {
                "images": [
                  {
                    "image_url": {
                      "url": "data:image/png;base64,{{Convert.ToBase64String(imageBytes)}}"
                    }
                  }
                ],
                "annotations": [
                  {
                    "url": "https://openrouter.ai/docs"
                  }
                ]
              }
            }
          ]
        }
        """;
        await using Stream input = new MemoryStream(Encoding.UTF8.GetBytes(responseJson), writable: false);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ProviderResponseImageDecodeResult result = new();

        await decoder.DecodeAsync(input, output, result, CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(imageBytes);
    }

    [Fact]
    public async Task DecodeAsync_WithDuplicateOpenRouterImage_DecodesFirstImageOnly()
    {
        byte[] firstImageBytes = [1, 2, 3, 4];
        byte[] duplicateImageBytes = [5, 6, 7, 8];
        string responseJson = $$"""
        {
          "content": {
            "image_url": {
              "url": "data:image/png;base64,{{Convert.ToBase64String(firstImageBytes)}}"
            }
          },
          "images": [
            {
              "image_url": {
                "url": "data:image/png;base64,{{Convert.ToBase64String(duplicateImageBytes)}}"
              }
            }
          ]
        }
        """;
        await using Stream input = new MemoryStream(Encoding.UTF8.GetBytes(responseJson), writable: false);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ProviderResponseImageDecodeResult result = new();

        await decoder.DecodeAsync(input, output, result, CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(firstImageBytes);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("image_url")]
    public async Task DecodeAsync_WithOpenRouterDirectDataUrlProperty_DecodesImage(
        string propertyName)
    {
        byte[] imageBytes = [1, 2, 3, 4];
        string responseJson = $$"""{ "{{propertyName}}": "data:image/png;base64,{{Convert.ToBase64String(imageBytes)}}" }""";
        await using Stream input = new MemoryStream(Encoding.UTF8.GetBytes(responseJson), writable: false);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ProviderResponseImageDecodeResult result = new();

        await decoder.DecodeAsync(input, output, result, CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(imageBytes);
    }

    [Theory]
    [InlineData("\\u002B////w\\u003D\\u003D", 1)]
    [InlineData("+\\/\\/\\/\\/w==", 3)]
    [InlineData("\\u002b\\u002f///w==", 19)]
    [InlineData("+//\\n//w==", 1)]
    [InlineData("+//\\r\\n//w==", 3)]
    [InlineData("+\\t////w==", 1)]
    [InlineData("+\\u000a////w==", 1)]
    public async Task DecodeAsync_WithEscapedBase64AcrossReadBoundaries_WritesOriginalImage(
        string escapedBase64,
        int maximumReadSize)
    {
        byte[] imageBytes = [251, 255, 255, 255];
        string responseJson = $$"""{ "data": [{ "b64_json": "{{escapedBase64}}" }] }""";
        await using Stream input = new ChunkedMemoryStream(
            Encoding.UTF8.GetBytes(responseJson),
            maximumReadSize);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ProviderResponseImageDecodeResult result = new();

        await decoder.DecodeAsync(input, output, result, CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(imageBytes);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("image_url")]
    [InlineData("url")]
    public async Task DecodeAsync_WithEscapedImageDataUrl_WritesOriginalImage(string propertyName)
    {
        byte[] imageBytes = [251, 255, 255, 255];
        string responseJson = $$"""
        { "{{propertyName}}": "d\u0061ta:image\/png;base64,\u002B\/\/\/\/w\u003d\u003d" }
        """;
        await using Stream input = new ChunkedMemoryStream(
            Encoding.UTF8.GetBytes(responseJson),
            maximumReadSize: 1);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ProviderResponseImageDecodeResult result = new();

        await decoder.DecodeAsync(input, output, result, CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(imageBytes);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("url")]
    public async Task DecodeAsync_WithLongNonImageStringBeforeImage_DecodesOnlyImage(string propertyName)
    {
        const int TextLength = 1024;
        byte[] imageBytes = [1, 2, 3, 4];
        string responseJson = $$"""
        {
          "{{propertyName}}": "{{new string('A', TextLength)}}",
          "images": [{ "image_url": { "url": "data:image/png;base64,{{Convert.ToBase64String(imageBytes)}}" } }]
        }
        """;
        await using Stream input = new ChunkedMemoryStream(
            Encoding.UTF8.GetBytes(responseJson),
            maximumReadSize: 1);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ProviderResponseImageDecodeResult result = new();

        await decoder.DecodeAsync(input, output, result, CancellationToken.None);

        result.HasImage.Should().BeTrue();
        output.ToArray().Should().Equal(imageBytes);
    }

    [Theory]
    [InlineData("AQ==AAAA")]
    [InlineData("AA=A")]
    [InlineData("AAA")]
    [InlineData("AAAA$")]
    [InlineData("\\u012B////w==")]
    [InlineData("\\u002G////w==")]
    [InlineData("\\x2B////w==")]
    [InlineData("AAAA\\\"")]
    [InlineData("AAAA\\\\")]
    [InlineData("AAAA\\b")]
    [InlineData("AAAA\\f")]
    public async Task DecodeAsync_WithMalformedImageString_ThrowsInvalidDataException(string imageString)
    {
        string responseJson = $$"""{ "b64_json": "{{imageString}}" }""";
        await using Stream input = new ChunkedMemoryStream(
            Encoding.UTF8.GetBytes(responseJson),
            maximumReadSize: 1);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper());
        ProviderResponseImageDecodeResult result = new();

        Func<Task> act = () => decoder.DecodeAsync(input, output, result, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        result.HasImage.Should().BeFalse();
    }

    [Theory]
    [InlineData("+////w==")]
    [InlineData("\\u002B////w\\u003D\\u003D")]
    public async Task DecodeAsync_WithImageExceedingDecodedSizeLimit_ThrowsInvalidDataException(
        string imageString)
    {
        const long MaximumImageBytes = 3;
        string responseJson = $$"""{ "b64_json": "{{imageString}}" }""";
        await using Stream input = new ChunkedMemoryStream(
            Encoding.UTF8.GetBytes(responseJson),
            maximumReadSize: 1);
        using MemoryStream output = new();
        JsonBase64ProviderResponseImageDecoder decoder = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper(
                maxDecodedProviderResponseImageBytes: MaximumImageBytes));
        ProviderResponseImageDecodeResult result = new();

        Func<Task> act = () => decoder.DecodeAsync(input, output, result, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        result.HasImage.Should().BeFalse();
        output.Length.Should().BeLessThanOrEqualTo(MaximumImageBytes);
    }

    private sealed class ChunkedMemoryStream : MemoryStream
    {
        private readonly int _maximumReadSize;

        public ChunkedMemoryStream(
            byte[] content,
            int maximumReadSize)
            : base(content, writable: false)
        {
            _maximumReadSize = maximumReadSize;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return base.ReadAsync(
                buffer[..Math.Min(buffer.Length, _maximumReadSize)],
                cancellationToken);
        }
    }
}
