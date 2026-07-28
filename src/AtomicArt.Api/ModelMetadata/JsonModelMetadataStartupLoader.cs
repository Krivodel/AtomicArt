using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Api.ModelMetadata;

public static class JsonModelMetadataStartupLoader
{
    private const string SafeSourceName = "generation model metadata file";

    public static GenerationModelCatalogDto Load(
        string path,
        IGenerationModelCatalogJsonSource source)
    {
        return LoadDocument(path, source).Catalog;
    }

    public static GenerationModelMetadataStartupDocument LoadDocument(
        string path,
        IGenerationModelCatalogJsonSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(source);

        string json = source.Read(path);

        GenerationModelCatalogDto catalog =
            GenerationModelCatalogMetadataLoader.LoadJson(json, SafeSourceName);
        TestGenerationModelMetadata? testModel =
            TestGenerationModelMetadataLoader.LoadOptionalJson(
                json,
                SafeSourceName);

        return new GenerationModelMetadataStartupDocument(
            catalog,
            testModel);
    }
}
