using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public interface IImageModelOptionCatalog
{
    bool IsLoaded { get; }
    bool IsLoading { get; }
    event EventHandler? CatalogChanged;
    event EventHandler? LoadingChanged;

    void Clear();
    void Initialize(GenerationModelCatalogDto catalog);
    void SetLoading(bool isLoading);

    IReadOnlyList<ImageModelOption> GetModels();
}
