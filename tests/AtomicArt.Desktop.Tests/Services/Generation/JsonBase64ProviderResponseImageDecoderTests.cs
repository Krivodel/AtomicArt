using System.Text;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation;

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
        JsonBase64ProviderResponseImageDecoder decoder = new();

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
        JsonBase64ProviderResponseImageDecoder decoder = new();

        ProviderResponseImageDecodeResult result = new();
        await decoder.DecodeAsync(
            input,
            output,
            result,
            CancellationToken.None);

        result.HasImage.Should().BeFalse();
        output.Length.Should().Be(0);
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
