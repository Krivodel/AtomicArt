using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services.Generation;

public sealed record GenerationRunRequest
{
    public ImageGenerationRequestDto Request { get; }
    public GenerationStartSnapshot StartSnapshot { get; }
    public string ProviderCredential { get; }

    public GenerationRunRequest(
        ImageGenerationRequestDto request,
        GenerationStartSnapshot startSnapshot,
        string? providerCredential)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(startSnapshot);

        Request = request;
        StartSnapshot = startSnapshot;
        ProviderCredential = providerCredential ?? string.Empty;
    }
}
