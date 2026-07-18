namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal sealed record GoogleInteractionsNoImageDiagnostics(
    string Category,
    string? Status,
    bool HasOutputImage,
    bool HasOutput,
    bool HasOutputImages,
    bool HasStepsTextContent,
    bool HasModelOutputTextContent,
    bool HasContentTextContent,
    int TextContentLength,
    int TextContentItemCount);
