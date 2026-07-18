using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Api.Tests.ModelMetadata;

internal sealed class FixedGenerationModelCatalogJsonSource : IGenerationModelCatalogJsonSource
{
    private readonly string _json;

    public FixedGenerationModelCatalogJsonSource(string json)
    {
        _json = json;
    }

    public string Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return _json;
    }
}
