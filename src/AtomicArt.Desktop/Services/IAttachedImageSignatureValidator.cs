namespace AtomicArt.Desktop.Services;

public interface IAttachedImageSignatureValidator
{
    bool TryGetContentType(string fileName, ReadOnlySpan<byte> content, out string contentType);

    bool MatchesSignature(string contentType, ReadOnlySpan<byte> content);
}
