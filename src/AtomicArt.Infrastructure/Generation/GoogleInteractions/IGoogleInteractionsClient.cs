namespace AtomicArt.Infrastructure.Generation.GoogleInteractions;

internal interface IGoogleInteractionsClient
{
    Task<string> CreateInteractionAsync(
        string requestJson,
        string providerCredential,
        CancellationToken ct);
}
