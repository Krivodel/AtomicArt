using System.Buffers;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Contracts.Generation;

namespace AtomicArt.Infrastructure.Generation.OpenRouter;

internal sealed class OpenRouterProviderGenerationStream : IProviderGenerationStream
{
    public string ContentType { get; }
    public ProviderGenerationSummary? Summary { get; private set; }

    private readonly OpenRouterImageResponse _response;
    private readonly long _maximumProviderResponseBytes;
    private readonly int _responseBufferSize;
    private readonly bool _usesChatCompletions;

    public OpenRouterProviderGenerationStream(
        OpenRouterImageResponse response,
        long maximumProviderResponseBytes,
        int responseBufferSize,
        bool usesChatCompletions = false)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumProviderResponseBytes, 1L);
        ArgumentOutOfRangeException.ThrowIfLessThan(responseBufferSize, 1);
        _maximumProviderResponseBytes = maximumProviderResponseBytes;
        _responseBufferSize = responseBufferSize;
        _usesChatCompletions = usesChatCompletions;
        ContentType = response.Response.Content.Headers.ContentType?.ToString() ?? "application/json";
    }

    public async Task CopyToAsync(Stream destination, long maximumBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1L);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(_responseBufferSize);
        OpenRouterImageUsageReader usageReader = new();
        OpenRouterChatCompletionResponseTransformer? chatTransformer =
            _usesChatCompletions ? new OpenRouterChatCompletionResponseTransformer() : null;
        long totalBytes = 0L;
        long effectiveMaximumBytes = Math.Min(maximumBytes, _maximumProviderResponseBytes);

        try
        {
            while (true)
            {
                int bytesRead = await _response.Content
                    .ReadAsync(buffer.AsMemory(0, _responseBufferSize), ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;

                if (totalBytes > effectiveMaximumBytes)
                {
                    throw new OpenRouterImageException(
                        ImageGenerationProviderFailureKind.InvalidResponse,
                        "The generation provider response exceeded its limit.");
                }

                ReadOnlyMemory<byte> responseBytes = buffer.AsMemory(0, bytesRead);
                usageReader.Append(responseBytes.Span);

                if (chatTransformer is null)
                {
                    await destination.WriteAsync(responseBytes, ct).ConfigureAwait(false);
                }
                else
                {
                    await chatTransformer.AppendAsync(responseBytes, ct)
                        .ConfigureAwait(false);
                }
            }

            string contentType = chatTransformer is null
                ? GenerationImageContentTypes.Png
                : await chatTransformer.CompleteAsync(destination, ct).ConfigureAwait(false);
            Summary = new ProviderGenerationSummary(
                "completed",
                1,
                [contentType],
                null,
                usageReader.ReadPrice());
        }
        finally
        {
            chatTransformer?.Dispose();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public ValueTask DisposeAsync()
    {
        return _response.DisposeAsync();
    }
}
