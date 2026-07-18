using AtomicArt.Contracts.Generation;
using CommonApiModelMetadataTestCatalog = AtomicArt.Tests.Common.Generation.ApiModelMetadataTestCatalog;

namespace AtomicArt.Desktop.Tests.Generation;

internal static class ApiModelMetadataTestCatalog
{
    public static string NanoBanana2ModelId => CommonApiModelMetadataTestCatalog.NanoBanana2ModelId;
    public static string NanoBananaProModelId => CommonApiModelMetadataTestCatalog.NanoBananaProModelId;
    public static string NanoBanana2DisplayName => CommonApiModelMetadataTestCatalog.NanoBanana2DisplayName;
    public static string NanoBanana2Resolution => "1024x1024";

    public static GenerationModelCatalogDto LoadCatalog()
    {
        return CommonApiModelMetadataTestCatalog.LoadCatalog();
    }

    public static GenerationModelMetadataDto LoadNanoBanana2Metadata()
    {
        return CommonApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
    }

    public static GenerationModelMetadataDto LoadNanoBananaProMetadata()
    {
        return CommonApiModelMetadataTestCatalog.LoadNanoBananaProMetadata();
    }
}
