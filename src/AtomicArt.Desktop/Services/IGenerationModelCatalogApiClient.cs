using AtomicArt.Contracts.Generation;

namespace AtomicArt.Desktop.Services;

public interface IGenerationModelCatalogApiClient
{
    Task<GenerationModelCatalogDto> GetCatalogAsync(CancellationToken ct = default);
}
