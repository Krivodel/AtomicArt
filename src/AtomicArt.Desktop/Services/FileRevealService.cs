using System.ComponentModel;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services;

public sealed class FileRevealService : IFileRevealService
{
    private const string RevealFailedMessage = "File reveal failed.";

    private readonly ITrustedImageFileService _trustedImageFileService;
    private readonly IFileRevealPlatform _fileRevealPlatform;

    public FileRevealService(
        ITrustedImageFileService trustedImageFileService,
        IFileRevealPlatform fileRevealPlatform)
    {
        ArgumentNullException.ThrowIfNull(trustedImageFileService);
        ArgumentNullException.ThrowIfNull(fileRevealPlatform);

        _trustedImageFileService = trustedImageFileService;
        _fileRevealPlatform = fileRevealPlatform;
    }

    public async Task RevealAsync(
        string? path,
        string modelId,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string fullPath = _trustedImageFileService.GetTrustedImagePath(path, modelId);

        try
        {
            await _fileRevealPlatform
                .RevealAsync(fullPath, windowMode, ct)
                .ConfigureAwait(false);
        }
        catch (Win32Exception ex)
        {
            throw new FileRevealException(RevealFailedMessage, ex);
        }
    }
}
