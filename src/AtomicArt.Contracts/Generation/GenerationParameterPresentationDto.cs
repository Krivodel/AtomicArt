namespace AtomicArt.Contracts.Generation;

public sealed record GenerationParameterPresentationDto(
    string LabelLocalizationKey,
    IReadOnlyDictionary<string, string>? ValueLocalizationKeys = null);
