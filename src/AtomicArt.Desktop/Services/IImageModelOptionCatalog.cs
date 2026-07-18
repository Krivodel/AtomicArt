using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public interface IImageModelOptionCatalog
{
    bool IsLoaded { get; }

    void Clear();
    void Initialize(GenerationModelCatalogDto catalog);

    IReadOnlyList<ImageModelOption> GetModels();
}
