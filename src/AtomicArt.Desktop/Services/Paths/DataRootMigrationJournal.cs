namespace AtomicArt.Desktop.Services.Paths;

internal sealed record DataRootMigrationJournal
{
    public required string SourceRootDirectory { get; init; }
    public required string DestinationRootDirectory { get; init; }
    public required DataRootMigrationStage Stage { get; init; }
    public required IReadOnlyList<string> Directories { get; init; }
    public required IReadOnlyList<DataRootMigrationFile> Files { get; init; }
}
