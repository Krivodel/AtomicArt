using System.Text.Json;

using AtomicArt.Contracts.Generation;
using AtomicArt.Infrastructure.Generation;

namespace AtomicArt.Tests.Common.Generation;

public static class ApiModelMetadataTestCatalog
{
    private const string NanoBanana2ModelIdValue = "nano-banana-2";
    private const string NanoBananaProModelIdValue = "nano-banana-pro";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string NanoBanana2ModelId => LoadNanoBanana2Metadata().Id;
    public static string NanoBananaProModelId => LoadNanoBananaProMetadata().Id;
    public static string NanoBanana2DisplayName => LoadNanoBanana2Metadata().DisplayName;

    public static GenerationModelCatalogDto LoadCatalog()
    {
        string json = File.ReadAllText(GetMetadataPath());
        GenerationModelCatalogDto? catalog = JsonSerializer.Deserialize<GenerationModelCatalogDto>(json, JsonOptions);

        return catalog ?? throw new InvalidOperationException("Generation model catalog test JSON is missing.");
    }

    public static GenerationModelMetadataDto LoadNanoBanana2Metadata()
    {
        return LoadById(NanoBanana2ModelIdValue);
    }

    public static GenerationModelMetadataDto LoadNanoBananaProMetadata()
    {
        return LoadById(NanoBananaProModelIdValue);
    }

    public static string GetApiContentRoot()
    {
        return FindRepositoryFileDirectory(Path.Combine(
            "src",
            "AtomicArt.Api",
            "AtomicArt.Api.csproj"));
    }

    public static string GetMetadataPath()
    {
        string relativePath = Path.Combine(
            "src",
            GenerationModelCatalogDefaults.SourceProjectName,
            GenerationModelCatalogDefaults.RelativePath);

        return TestRepositoryFiles.TryFindFromCurrentOrBaseDirectory(relativePath)
            ?? throw new InvalidOperationException("Generation model metadata JSON was not found.");
    }

    private static string FindRepositoryFileDirectory(string relativePath)
    {
        string filePath = TestRepositoryFiles.TryFindFromCurrentOrBaseDirectory(relativePath)
            ?? throw new InvalidOperationException($"Repository file '{relativePath}' was not found.");

        return Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("Repository file directory was not found.");
    }

    private static GenerationModelMetadataDto LoadById(string modelId)
    {
        return LoadCatalog().Models.Single(model =>
            string.Equals(model.Id, modelId, StringComparison.Ordinal));
    }
}
