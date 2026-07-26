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
        CrossPlatformFileReveal.Reveal(filePath);

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
}
