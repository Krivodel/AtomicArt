namespace AtomicArt.Desktop.Services.Dlss5;

public sealed record Dlss5RuntimeFile(
    string ArchivePath,
    string RelativePath,
    long Length,
    string? Sha256,
    bool IsOptional = false);
