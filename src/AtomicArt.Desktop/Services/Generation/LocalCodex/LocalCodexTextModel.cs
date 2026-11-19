namespace AtomicArt.Desktop.Services.Generation.LocalCodex;

public sealed record LocalCodexTextModel(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ReasoningEfforts,
    string? DefaultReasoningEffort);
