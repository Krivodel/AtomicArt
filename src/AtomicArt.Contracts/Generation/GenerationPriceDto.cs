namespace AtomicArt.Contracts.Generation;

public sealed record GenerationPriceDto(
    decimal Amount,
    string CurrencyCode,
    string Source);
