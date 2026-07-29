using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Localization;

internal sealed class LocalizationTemplateDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }
    [JsonPropertyName("culture")]
    public required string Culture { get; init; }
    [JsonPropertyName("strings")]
    public JsonElement Strings { get; init; }
}
