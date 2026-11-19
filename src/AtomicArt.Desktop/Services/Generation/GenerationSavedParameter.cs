using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationSavedParameter(
    [property: JsonRequired] JsonElement Value,
    string? LabelLocalizationKey = null,
    string? ValueLocalizationKey = null);
