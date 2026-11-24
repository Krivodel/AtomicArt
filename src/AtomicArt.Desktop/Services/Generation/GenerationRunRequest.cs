using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Generation.LocalCodex;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationRunRequest
{
    public ImageGenerationRequestDto Request { get; }
    public GenerationStartSnapshot StartSnapshot { get; }
    public string ProviderCredential { get; }
    public LocalCodexTurnOptions? CodexTurnOptions { get; }
    public GenerationChatRun? ChatRun { get; init; }
    public IReadOnlyList<GenerationReference>? ChatReferences { get; init; }

    public GenerationRunRequest(
        ImageGenerationRequestDto request,
        GenerationStartSnapshot startSnapshot,
        string? providerCredential,
        LocalCodexTurnOptions? codexTurnOptions = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(startSnapshot);

        Request = request;
        StartSnapshot = startSnapshot;
        ProviderCredential = providerCredential ?? string.Empty;
        CodexTurnOptions = codexTurnOptions;
    }
}
