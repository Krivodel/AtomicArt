using System.Diagnostics;

namespace Pica.Viewer.Services;

internal sealed class CrossPlatformFileActions : PlatformFileActions
{
    public override bool SupportsOpenWith => false;

    protected override IReadOnlyList<OpenWithApplication> GetOpenWithApplicationsCore(
        string filePath)
    {
        return new List<OpenWithApplication>();
    }

    protected override Task RevealInFolderCoreAsync(
        string filePath,
        CancellationToken ct)
    {
        ProcessStartInfo startInfo = OperatingSystem.IsMacOS()
            ? CreateMacRevealStartInfo(filePath)
            : CreateLinuxRevealStartInfo(filePath);
        using Process? process = Process.Start(startInfo);

        return Task.CompletedTask;
    }

    protected override Task OpenWithCoreAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    protected override Task ChooseApplicationCoreAsync(
        string filePath,
        CancellationToken ct)
    {
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
