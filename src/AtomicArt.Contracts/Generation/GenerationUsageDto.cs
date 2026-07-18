namespace AtomicArt.Contracts.Generation;

public sealed record GenerationUsageDto(
    int TotalTokens,
    int? TotalInputTokens = null,
    int? TotalOutputTokens = null,
    IReadOnlyList<GenerationModalityTokensDto>? InputTokensByModality = null,
    IReadOnlyList<GenerationModalityTokensDto>? OutputTokensByModality = null,
    int? TotalThoughtTokens = null,
    int? TotalToolUseTokens = null,
    int? TotalCachedTokens = null);
