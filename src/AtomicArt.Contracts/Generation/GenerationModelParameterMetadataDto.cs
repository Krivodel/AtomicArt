using System.Text.Json;

namespace AtomicArt.Contracts.Generation;

public sealed record GenerationModelParameterMetadataDto(
    string Name,
    string Type,
    bool Required,
    JsonElement? DefaultValue = null,
    double? Minimum = null,
    double? Maximum = null,
    double? Step = null,
    IReadOnlyList<JsonElement>? AllowedValues = null);
