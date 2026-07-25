namespace AtomicArt.Desktop.Services.Paths;

internal sealed record DataRootMigrationFile
{
    public required string RelativePath { get; init; }
    public required long Length { get; init; }
    public required DateTime CreationTimeUtc { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
    public required FileAttributes Attributes { get; init; }
    public string? Sha256 { get; init; }
}
