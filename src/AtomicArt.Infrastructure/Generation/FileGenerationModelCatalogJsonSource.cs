using System.Text;

namespace AtomicArt.Infrastructure.Generation;

public sealed class FileGenerationModelCatalogJsonSource : IGenerationModelCatalogJsonSource
{
    public string Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "The generation model metadata file was not found.");
        }

        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (IOException)
        {
            throw new InvalidOperationException(
                "The generation model metadata file could not be read because of an I/O error.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Access to the generation model metadata file was denied.");
        }
    }
}
