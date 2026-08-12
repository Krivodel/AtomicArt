namespace AtomicArt.Desktop.Services.Paths;

internal sealed record AtomicArtDataRootBootstrapState
{
    public required string RootDirectory { get; init; }
    public bool? IsInitialRootDirectorySelectionCompleted { get; init; }
}
