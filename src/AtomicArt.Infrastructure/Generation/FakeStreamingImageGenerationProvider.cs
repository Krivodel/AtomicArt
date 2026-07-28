using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation;

internal sealed class FakeStreamingImageGenerationProvider
    : IProviderStreamingImageGenerationProvider
{
    public string Provider => GenerationProviderIds.Test;

    private readonly IStreamingPlaceholderImageProvider
        _placeholderImageProvider;
    private readonly TestGenerationOptions _options;

    public FakeStreamingImageGenerationProvider(
        IStreamingPlaceholderImageProvider placeholderImageProvider,
        IOptions<TestGenerationOptions> options)
    {
        _placeholderImageProvider = placeholderImageProvider
            ?? throw new ArgumentNullException(nameof(placeholderImageProvider));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task<IProviderGenerationStream> CreateStreamAsync(
        StreamingGenerationProviderContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        StreamingPlaceholderImage image = await _placeholderImageProvider
            .OpenNextAsync(context.Request.ModelId, 0, ct)
            .ConfigureAwait(false);

        return new FakeProviderGenerationStream(
            image,
            _options);
    }

    private sealed class FakeProviderGenerationStream : IProviderGenerationStream
    {
        public string ContentType => "application/json";
        public ProviderGenerationSummary? Summary { get; private set; }

        private readonly StreamingPlaceholderImage _image;
        private readonly int _generationDelayMilliseconds;
        private readonly int _inputBlockSize;
        private readonly int _outputBlockSize;

        public FakeProviderGenerationStream(
            StreamingPlaceholderImage image,
            TestGenerationOptions options)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
            ArgumentNullException.ThrowIfNull(options);
            _generationDelayMilliseconds =
                options.GenerationDelayMilliseconds;
            _inputBlockSize = options.Base64InputBufferSize;
            _outputBlockSize = options.Base64OutputBufferSize;
        }

        public async Task CopyToAsync(
            Stream destination,
            long maximumBytes,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1L);

            byte[] prefix = Encoding.UTF8.GetBytes(
                $"{{\"status\":\"completed\",\"output\":[{{\"type\":\"image\",\"mime_type\":{JsonSerializer.Serialize(_image.ContentType)},\"data\":\"");
            byte[] suffix = "\"}]}"u8.ToArray();
            long encodedLength =
                ((_image.ContentLength + 2L) / 3L) * 4L;
            long totalLength = prefix.LongLength + encodedLength + suffix.LongLength;

            if (totalLength > maximumBytes)
            {
                throw new ImageGenerationProviderException(
                    ImageGenerationProviderFailureKind.InvalidResponse,
                    "The test provider response exceeded its limit.");
            }

            await Task.Delay(
                    TimeSpan.FromMilliseconds(_generationDelayMilliseconds),
                    ct)
                .ConfigureAwait(false);
            await destination.WriteAsync(prefix, ct).ConfigureAwait(false);
            byte[] inputBuffer = ArrayPool<byte>.Shared.Rent(_inputBlockSize);
            byte[] outputBuffer = ArrayPool<byte>.Shared.Rent(_outputBlockSize);

            try
            {
                long totalBytesRead = 0L;

                while (true)
                {
                    int count = await FillBufferAsync(
                            _image.Content,
                            inputBuffer,
                            ct)
                        .ConfigureAwait(false);

                    if (count == 0)
                    {
                        break;
                    }

                    totalBytesRead += count;
                    OperationStatus status = Base64.EncodeToUtf8(
                        inputBuffer.AsSpan(0, count),
                        outputBuffer,
                        out int consumed,
                        out int written);

                    if (status != OperationStatus.Done || consumed != count)
                    {
                        throw new InvalidOperationException(
                            "Test image Base64 encoding did not consume the complete block.");
                    }

                    await destination
                        .WriteAsync(outputBuffer.AsMemory(0, written), ct)
                        .ConfigureAwait(false);
                }

                if (totalBytesRead != _image.ContentLength)
                {
                    throw new InvalidDataException(
                        "Test image length changed while streaming.");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(inputBuffer);
                ArrayPool<byte>.Shared.Return(outputBuffer);
            }

            await destination.WriteAsync(suffix, ct).ConfigureAwait(false);

            Summary = new ProviderGenerationSummary(
                "completed",
                1,
                new List<string>
                {
                    _image.ContentType
                },
                null);
        }

        public ValueTask DisposeAsync()
        {
            return _image.DisposeAsync();
        }

        private async Task<int> FillBufferAsync(
            Stream source,
            byte[] buffer,
            CancellationToken ct)
        {
            int totalBytesRead = 0;

            while (totalBytesRead < _inputBlockSize)
            {
                int bytesRead = await source
                    .ReadAsync(
                        buffer.AsMemory(
                            totalBytesRead,
                            _inputBlockSize - totalBytesRead),
                        ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }
    }
}
