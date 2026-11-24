namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationChatBatch(
    GenerationChatHistory History,
    GenerationChatMessage UserMessage,
    IReadOnlyList<GenerationChatRun> Runs);
