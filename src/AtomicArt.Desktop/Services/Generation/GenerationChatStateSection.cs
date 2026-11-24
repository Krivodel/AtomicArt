using AtomicArt.Desktop.Services.State;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationChatStateSection : StateSection<GenerationChatState>
{
    public override StateStorageArea StorageArea => StateStorageArea.Chats;

    internal GenerationChatStateSection(Guid id)
        : base(id.ToString("N"), string.Concat(id.ToString("N"), ".json"), 1)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The chat identifier must not be empty.", nameof(id));
        }
    }

    protected override GenerationChatState NormalizePayload(GenerationChatState? state)
    {
        return state ?? new GenerationChatState();
    }
}
