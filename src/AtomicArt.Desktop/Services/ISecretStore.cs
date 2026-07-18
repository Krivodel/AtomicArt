namespace AtomicArt.Desktop.Services;

public interface ISecretStore
{
    Task<string?> GetSecretAsync(string key, CancellationToken ct);

    Task SetSecretAsync(string key, string value, CancellationToken ct);
}
