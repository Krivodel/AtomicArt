using System.Text.Json.Serialization;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationChatHistory
{
    [JsonInclude]
    public Guid Id { get; private set; } = Guid.NewGuid();
    public IReadOnlyList<GenerationChatMessage> Messages { get; }
    public bool IncludesCardTurn { get; }
    [JsonIgnore]
    public IReadOnlyList<string> ReferenceHashes { get; }

    public GenerationChatHistory(IReadOnlyList<GenerationChatMessage> messages, bool includesCardTurn = false)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Messages = Array.AsReadOnly(messages.Select(message => message with
        {
            Images = Array.AsReadOnly(message.Images.ToArray())
        }).ToArray());
        IncludesCardTurn = includesCardTurn;
        ReferenceHashes = Array.AsReadOnly(Messages.SelectMany(message => message.Images).Select(image => image.Hash)
            .Distinct(StringComparer.Ordinal).ToArray());
    }
}
