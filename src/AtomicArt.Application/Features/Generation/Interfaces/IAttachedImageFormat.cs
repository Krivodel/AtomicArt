namespace AtomicArt.Application.Features.Generation.Interfaces;

public interface IAttachedImageFormat
{
    string ContentType { get; }

    bool MatchesContentType(string contentType);

    bool MatchesSignature(ReadOnlySpan<byte> content);
}
