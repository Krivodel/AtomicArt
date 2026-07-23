namespace AtomicArt.Desktop.Services.Generation;

public interface IProviderResponseImageDecoder
{
    bool CanDecode(string providerId, string contentType);

    Task DecodeAsync(
        Stream providerResponse,
        Stream imageDestination,
        ProviderResponseImageDecodeResult result,
        CancellationToken ct);
}
