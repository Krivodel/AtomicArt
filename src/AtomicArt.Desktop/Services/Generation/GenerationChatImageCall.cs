namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationChatImageCall(
    string ImageHash,
    string? RevisedPrompt,
    bool? TransparentBackground,
    string? GenerationId);
