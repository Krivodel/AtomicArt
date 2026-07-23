namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelTransportLimitsDto(
    long MaxRequestBytes,
    long MaxResponseBytes,
    int MaxResultCount,
    int MaxStatisticsBytes,
    int MaxStructureDepth,
    int MaxDiagnosticTextCharacters,
    IReadOnlyList<string> AllowedResponseContentTypes);
