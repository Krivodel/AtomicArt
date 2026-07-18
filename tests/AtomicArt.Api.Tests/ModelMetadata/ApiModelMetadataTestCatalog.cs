using AtomicArt.Api.ModelMetadata;
using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;
using CommonApiModelMetadataTestCatalog = AtomicArt.Tests.Common.Generation.ApiModelMetadataTestCatalog;

namespace AtomicArt.Api.Tests.ModelMetadata;

internal static class ApiModelMetadataTestCatalog
{
    public static string NanoBanana2ModelId => CommonApiModelMetadataTestCatalog.NanoBanana2ModelId;
    public static string NanoBananaProModelId => CommonApiModelMetadataTestCatalog.NanoBananaProModelId;

    public static GenerationModelCatalogDto LoadCatalog()
    {
        string metadataPath = CommonApiModelMetadataTestCatalog.GetMetadataPath();
        string json = File.ReadAllText(metadataPath);

        return JsonModelMetadataStartupLoader.Load(
            metadataPath,
            new TestGenerationModelCatalogJsonSource(json));
    }

    public static GenerationModelMetadataDto LoadNanoBanana2Metadata()
    {
        return CommonApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
    }

    public static string GetContentRoot()
    {
        return CommonApiModelMetadataTestCatalog.GetApiContentRoot();
    }

    public static string GetMetadataPath()
    {
        return CommonApiModelMetadataTestCatalog.GetMetadataPath();
    }

    private sealed class TestGenerationModelCatalogJsonSource : IGenerationModelCatalogJsonSource
    {
        private readonly string _json;

        public TestGenerationModelCatalogJsonSource(string json)
        {
            _json = json;
        }

        public string Read(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            return _json;
        }
    }
}
