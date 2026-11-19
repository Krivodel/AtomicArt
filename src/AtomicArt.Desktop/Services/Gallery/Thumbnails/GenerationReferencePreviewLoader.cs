using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GenerationReferencePreviewLoader
{
    private const int MaximumPreviewPixels = 96;

    private readonly IGenerationReferenceStore _references;
    private readonly ILogger<GenerationReferencePreviewLoader> _logger;
    private readonly SemaphoreSlim _decodeSlots = new(2, 2);

    public GenerationReferencePreviewLoader(
        IGenerationReferenceStore references,
        ILogger<GenerationReferencePreviewLoader> logger)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Bitmap?> LoadAsync(GenerationReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        await _decodeSlots.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await _references.ReadAsync<Bitmap>(
                reference,
                async (stream, token) => await Task.Run(
                    () => PreviewBitmapDecoder.Decode(stream, MaximumPreviewPixels, token, MaximumPreviewPixels),
                    token).ConfigureAwait(false),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogDebug(ex, "Could not load a generation reference preview.");
            return null;
        }
        finally
        {
            _decodeSlots.Release();
        }
    }
}
