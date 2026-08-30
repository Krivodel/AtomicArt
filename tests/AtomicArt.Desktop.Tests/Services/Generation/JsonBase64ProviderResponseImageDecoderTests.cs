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
