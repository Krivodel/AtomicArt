using System.ComponentModel;
using System.Diagnostics;

namespace AtomicArt.Desktop.Services;

public sealed class FileRevealService : IFileRevealService
{
    private const string ExplorerFileName = "explorer.exe";
    private const string ExplorerSelectArgument = "/select,";
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
                RevealOnWindows(fullPath);
            }
            catch (Win32Exception ex)
            {
                throw new FileRevealException(RevealFailedMessage, ex);
            }
        }

        return Task.CompletedTask;
    }

    private static void RevealOnWindows(string fullPath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = ExplorerFileName,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(ExplorerSelectArgument);
        startInfo.ArgumentList.Add(fullPath);
        Process.Start(startInfo)?.Dispose();
    }
}
