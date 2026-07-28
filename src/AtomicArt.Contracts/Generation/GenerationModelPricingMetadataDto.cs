namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelPricingMetadataDto(
    string CurrencyCode,
    decimal InputTokenUsdPerMillion,
    decimal TextOutputTokenUsdPerMillion,
    decimal ImageOutputTokenUsdPerMillion,
    decimal EstimatedCharactersPerTextToken,
    int InputImageTokens,
    IReadOnlyDictionary<string, int> OutputImageTokensByResolution);
