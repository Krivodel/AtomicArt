using System.ComponentModel;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services;

public sealed class FileRevealService : IFileRevealService
{
    private const string RevealFailedMessage = "File reveal failed.";

    private readonly ITrustedImageFileService _trustedImageFileService;

    public FileRevealService(ITrustedImageFileService trustedImageFileService)
    {
        ArgumentNullException.ThrowIfNull(trustedImageFileService);

        _trustedImageFileService = trustedImageFileService;
    }

    public Task RevealAsync(string? path, string modelId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string fullPath = _trustedImageFileService.GetTrustedImagePath(path, modelId);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                WindowsFileReveal.Reveal(fullPath);
            }
            catch (Win32Exception ex)
            {
                throw new FileRevealException(RevealFailedMessage, ex);
            }
        }

        return Task.CompletedTask;
    }
}
