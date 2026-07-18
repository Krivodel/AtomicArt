namespace AtomicArt.Infrastructure.Generation;

public interface IGenerationModelCatalogJsonSource
{
    string Read(string path);
}
