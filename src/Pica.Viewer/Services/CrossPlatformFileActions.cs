using System.Diagnostics;

namespace Pica.Viewer.Services;

internal sealed class CrossPlatformFileActions : IPlatformFileActions
{
    public bool SupportsOpenWith => false;

    public IReadOnlyList<OpenWithApplication> GetOpenWithApplications(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return new List<OpenWithApplication>();
    }

    public Task RevealInFolderAsync(string filePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        ProcessStartInfo startInfo = OperatingSystem.IsMacOS()
            ? CreateMacRevealStartInfo(filePath)
            : CreateLinuxRevealStartInfo(filePath);
        using Process? process = Process.Start(startInfo);

        return Task.CompletedTask;
    }

    public Task OpenWithAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(application);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    public Task ChooseApplicationAsync(string filePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    private static ProcessStartInfo CreateMacRevealStartInfo(string filePath)
    {
        ProcessStartInfo startInfo = new("/usr/bin/open")
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-R");
        startInfo.ArgumentList.Add(filePath);

        return startInfo;
    }

    private static ProcessStartInfo CreateLinuxRevealStartInfo(string filePath)
    {
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The image directory could not be determined.");
        ProcessStartInfo startInfo = new("xdg-open")
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(directoryPath);

        return startInfo;
    }
}
