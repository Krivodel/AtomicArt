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
    public IReadOnlyList<Guid> AncestorCardIds { get; init; } = Array.Empty<Guid>();
    [JsonIgnore]
    public GenerationChatHistory? Context { get; init; }
}
