namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelTemperatureMetadataDto(
    double Minimum,
    double Maximum,
    double Default,
    double Step);
