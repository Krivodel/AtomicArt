namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal interface IGoogleInteractionsClient
{
    Task<GoogleInteractionsStreamingResponse> CreateInteractionStreamAsync(
        HttpContent content,
        string providerCredential,
        CancellationToken ct);
}
