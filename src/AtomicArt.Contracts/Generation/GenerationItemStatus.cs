using System.Text.Json.Serialization;

namespace AtomicArt.Contracts.Generation;

[JsonConverter(typeof(JsonStringEnumConverter<GenerationItemStatus>))]
public enum GenerationItemStatus
{
    Generated = 1,
    Generating = 2,
    Failed = 3
}
