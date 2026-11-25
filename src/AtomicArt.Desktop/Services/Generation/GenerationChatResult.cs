using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationChatResult(string Text, GenerationReference? Image, Guid? StorageLeaseId = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsTextReply { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GenerationChatImageCall? ImageGeneration { get; init; }
}
