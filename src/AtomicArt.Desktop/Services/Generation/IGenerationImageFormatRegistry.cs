namespace AtomicArt.Desktop.Services.Generation;

public interface IGenerationImageFormatRegistry
{
    IReadOnlyCollection<IGenerationImageFormat> Formats { get; }

    bool TryGetByContentType(string? contentType, out IGenerationImageFormat? format);

    bool TryGetByFileName(string fileName, out IGenerationImageFormat? format);
}
