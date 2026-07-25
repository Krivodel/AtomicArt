namespace AtomicArt.Desktop.Services.Paths;

internal sealed record DataRootMigrationPlan
{
    public required string SourceRootDirectory { get; init; }
    public required string DestinationRootDirectory { get; init; }
    public required IReadOnlyList<string> RelativeDirectories { get; init; }
    public required IReadOnlyList<DataRootMigrationFile> Files { get; init; }
    public required long TotalBytes { get; init; }
}
