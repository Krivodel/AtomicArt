namespace AtomicArt.Infrastructure.Benchmarks;

internal static class GoogleStreamingResponseAnalyzerBenchmarkLimits
{
    public const int MaximumDiagnosticTextCharacters = 512;
    public const int MaximumFilteredResponseBytes = 4 * 1024 * 1024;
    public const int MaximumStructureDepth = 64;
}
