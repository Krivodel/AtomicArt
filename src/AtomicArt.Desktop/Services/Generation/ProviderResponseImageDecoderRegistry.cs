namespace AtomicArt.Desktop.Services.Generation;

public sealed class ProviderResponseImageDecoderRegistry
{
    private readonly IReadOnlyList<IProviderResponseImageDecoder> _decoders;

    public ProviderResponseImageDecoderRegistry(
        IEnumerable<IProviderResponseImageDecoder> decoders)
    {
        ArgumentNullException.ThrowIfNull(decoders);

        _decoders = decoders.ToList();
    }

    public IProviderResponseImageDecoder GetRequired(
        string providerId,
        string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        IProviderResponseImageDecoder? decoder = _decoders
            .SingleOrDefault(candidate => candidate.CanDecode(
                providerId,
                contentType));

        return decoder ?? throw new InvalidDataException(
            "No image decoder is registered for the provider response.");
    }
}
