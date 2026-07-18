namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationImageContentValidationResult
{
    public string ContentType { get; }
    public ReadOnlyMemory<byte> Bytes => _bytes;

    private readonly byte[] _bytes;

    internal GenerationImageContentValidationResult(string contentType, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(bytes);

        ContentType = contentType;
        _bytes = bytes;
    }
}
