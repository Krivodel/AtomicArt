namespace AtomicArt.Application.Features.Generation.Commands.CreateImageGeneration;

public interface IAttachedImageFormat
{
    string ContentType { get; }

    bool MatchesContentType(string contentType);

    bool MatchesSignature(ReadOnlySpan<byte> content);
}
