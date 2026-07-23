using System.Buffers;

using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;

namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed class GoogleProviderGenerationStream : IProviderGenerationStream
{
    private const int BufferSize = 65536;

    public string ContentType { get; }
    public ProviderGenerationSummary? Summary { get; private set; }

    private readonly GoogleInteractionsStreamingResponse _response;
    private readonly GoogleInteractionsResponseParser _responseParser;
    private readonly GoogleInteractionsFailureClassifier _failureClassifier;
    private readonly long _maximumProviderResponseBytes;
    private readonly int _maximumAnalyzedMetadataBytes;
    private readonly int _maximumStructureDepth;
    private readonly int _maximumDiagnosticTextCharacters;

    public GoogleProviderGenerationStream(
        GoogleInteractionsStreamingResponse response,
        GoogleInteractionsResponseParser responseParser,
        GoogleInteractionsFailureClassifier failureClassifier,
        long maximumProviderResponseBytes,
        int maximumAnalyzedMetadataBytes,
        int maximumStructureDepth,
        int maximumDiagnosticTextCharacters)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _responseParser = responseParser
            ?? throw new ArgumentNullException(nameof(responseParser));
        _failureClassifier = failureClassifier
            ?? throw new ArgumentNullException(nameof(failureClassifier));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumProviderResponseBytes,
            1L);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumAnalyzedMetadataBytes,
            1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumStructureDepth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumDiagnosticTextCharacters,
            1);
        _maximumProviderResponseBytes = maximumProviderResponseBytes;
        _maximumAnalyzedMetadataBytes = maximumAnalyzedMetadataBytes;
        _maximumStructureDepth = maximumStructureDepth;
        _maximumDiagnosticTextCharacters =
            maximumDiagnosticTextCharacters;
        ContentType = response.Response.Content.Headers.ContentType?.ToString()
            ?? "application/json";
    }

    public async Task CopyToAsync(
        Stream destination,
        long maximumBytes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1L);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long totalBytes = 0L;
        GoogleStreamingResponseAnalyzer analyzer = new(
            _responseParser,
            _failureClassifier,
            _maximumAnalyzedMetadataBytes,
            _maximumStructureDepth,
            _maximumDiagnosticTextCharacters);
        long effectiveMaximumBytes = Math.Min(
            maximumBytes,
            _maximumProviderResponseBytes);

        try
        {
            while (true)
            {
                int bytesRead = await _response.Content
                    .ReadAsync(buffer.AsMemory(0, BufferSize), ct)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                totalBytes += bytesRead;

                if (totalBytes > effectiveMaximumBytes)
                {
                    throw new ImageGenerationProviderException(
                        ImageGenerationProviderFailureKind.InvalidResponse,
                        "The generation provider response exceeded its limit.");
                }

                analyzer.Append(buffer.AsSpan(0, bytesRead));
                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), ct)
                    .ConfigureAwait(false);
            }

            Summary = analyzer.Complete();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public ValueTask DisposeAsync()
    {
        return _response.DisposeAsync();
    }
}
