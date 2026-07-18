namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed record GoogleInteractionsErrorDiagnostics(
    GoogleInteractionsErrorBodyKind BodyKind,
    int CharacterCount,
    int? ErrorCode,
    string? ErrorStatus,
    string? ErrorMessage);
