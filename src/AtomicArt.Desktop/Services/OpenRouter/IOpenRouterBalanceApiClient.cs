namespace AtomicArt.Desktop.Services.OpenRouter;

public interface IOpenRouterBalanceApiClient
{
    Task<decimal> GetBalanceAsync(string managementKey, CancellationToken ct);
}
