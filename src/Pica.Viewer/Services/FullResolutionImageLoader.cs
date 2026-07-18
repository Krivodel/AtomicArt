using Avalonia.Media.Imaging;

namespace Pica.Viewer.Services;

internal sealed class FullResolutionImageLoader
{
    private static readonly SemaphoreSlim DecodeLock = new(1, 1);

    public async Task<Bitmap> LoadAsync(string fullPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        await DecodeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await Task
                .Run(() => AvaloniaBitmapDecoder.DecodeFile(fullPath, ct), ct)
                .ConfigureAwait(false);
        }
        finally
        {
            DecodeLock.Release();
        }
    }
}
