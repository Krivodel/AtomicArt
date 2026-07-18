namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationImageFormatRegistry : IGenerationImageFormatRegistry
{
    private readonly IReadOnlyList<IGenerationImageFormat> _formats;

    public IReadOnlyCollection<IGenerationImageFormat> Formats => _formats;

    public GenerationImageFormatRegistry(IEnumerable<IGenerationImageFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        _formats = formats
            .OrderBy(format => format.ContentType, StringComparer.Ordinal)
            .ToList();
    }

    public bool TryGetByContentType(string? contentType, out IGenerationImageFormat? format)
    {
        format = null;

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        format = _formats.FirstOrDefault(candidateFormat =>
            candidateFormat.MatchesContentType(contentType));

        return format is not null;
    }

    public bool TryGetByFileName(string fileName, out IGenerationImageFormat? format)
    {
        format = null;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        format = _formats.FirstOrDefault(candidateFormat =>
            candidateFormat.MatchesFileName(fileName));

        return format is not null;
    }
}
