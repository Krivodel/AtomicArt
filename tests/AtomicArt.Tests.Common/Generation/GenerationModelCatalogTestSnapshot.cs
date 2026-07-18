using AtomicArt.Contracts.Generation;

namespace AtomicArt.Tests.Common.Generation;

public sealed record GenerationModelCatalogTestSnapshot(
    string Id,
    string Provider,
    string ProviderModelId,
    string PanelId,
    GenerationModelTemperatureMetadataDto Temperature,
    int OutputImageTokens);
