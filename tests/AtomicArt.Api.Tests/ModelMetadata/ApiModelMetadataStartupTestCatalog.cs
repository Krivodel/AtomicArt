using AtomicArt.Api.ModelMetadata;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.ModelMetadata;

internal static class ApiModelMetadataStartupTestCatalog
{
    public static GenerationModelCatalogDto LoadCatalog()
    {
        string metadataPath = ApiModelMetadataTestCatalog.GetMetadataPath();
        string json = File.ReadAllText(metadataPath);

        return JsonModelMetadataStartupLoader.Load(
            metadataPath,
            new FixedGenerationModelCatalogJsonSource(json));
    }
}
