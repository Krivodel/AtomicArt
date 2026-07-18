namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationImageFormat
{
    string ContentType { get; }
    string Extension { get; }

    bool MatchesContentType(string contentType);

    bool MatchesFileName(string fileName);

    bool MatchesSignature(ReadOnlySpan<byte> bytes);
}
