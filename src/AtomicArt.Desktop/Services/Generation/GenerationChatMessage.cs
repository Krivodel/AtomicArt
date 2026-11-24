namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationChatMessage(
    bool IsAssistant,
    string Text,
    DateTime CreatedAtUtc,
    IReadOnlyList<GenerationReference> Images,
    bool Failed = false);
