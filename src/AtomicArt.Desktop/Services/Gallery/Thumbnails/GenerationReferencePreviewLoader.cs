using Microsoft.Extensions.Logging;

using Avalonia;
using Avalonia.Media.Imaging;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GenerationReferencePreviewLoader : IDisposable
{
    private const int MaximumPreviewPixels = 96;
    private const long MaximumCacheBytes = 64L * 1024 * 1024;
    private const int MaximumConcurrentDecodes = 2;

    private readonly IGenerationReferenceStore _references;
    private readonly ILogger<GenerationReferencePreviewLoader> _logger;
    private readonly PreviewBitmapCache<PreviewKey, GenerationReferencePreviewLoader> _cache;

    public GenerationReferencePreviewLoader(
        IGenerationReferenceStore references,
        ILogger<GenerationReferencePreviewLoader> logger)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = new PreviewBitmapCache<PreviewKey, GenerationReferencePreviewLoader>(
            DecodeAsync, logger, MaximumCacheBytes, maximumConcurrentLoads: MaximumConcurrentDecodes);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    internal Task<GalleryPreviewBitmapLease?> AcquireAsync(
        GenerationReference reference, CancellationToken ct, int? maximumPreviewPixels = null)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _cache.AcquireAsync(new PreviewKey(reference, maximumPreviewPixels ?? MaximumPreviewPixels), ct);
    }

    internal Task<PreviewImageDimensions?> ReadDimensionsAsync(GenerationReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return ReadAsync<PreviewImageDimensions>(reference, (stream, token) =>
        {
            PixelSize size = PreviewBitmapDecoder.ReadPixelSize(stream, token);

            return new PreviewImageDimensions(size.Width, size.Height);
        }, ct);
    }

    private Task<Bitmap?> DecodeAsync(PreviewKey preview, CancellationToken ct)
    {
        return ReadAsync(preview.Reference,
            (stream, token) => PreviewBitmapDecoder.Decode(stream, preview.PixelLimit, token, preview.PixelLimit), ct);
    }

    private async Task<TResult?> ReadAsync<TResult>(GenerationReference reference,
        Func<Stream, CancellationToken, TResult> read, CancellationToken ct) where TResult : class
    {
        try
        {
            return await Task.Run(
                () => _references.ReadAsync<TResult>(
                    reference,
                    (stream, token) => Task.FromResult<TResult?>(read(stream, token)),
                    ct), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogDebug(ex, "Could not load a generation reference preview.");

            return null;
        }
    }

    private readonly record struct PreviewKey(GenerationReference Reference, int PixelLimit);
}
