using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationChatGroup(
    Guid Id,
    Guid SourceCardId,
    Guid ContextId,
    IReadOnlyList<string> ReferenceHashes)
{
    public DateTime CreatedAtUtc { get; init; }
    public int VariantOrdinal { get; init; }
    [JsonIgnore]
    public GenerationChatHistory? Context { get; init; }
}
