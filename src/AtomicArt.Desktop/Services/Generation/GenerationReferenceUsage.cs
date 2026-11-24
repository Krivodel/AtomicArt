namespace AtomicArt.Desktop.Services.Generation;

internal sealed record GenerationReferenceUsage(
    IReadOnlySet<string> PendingHashes,
    IReadOnlySet<string> ResultHashes,
    IReadOnlySet<string> ReadHashes);
