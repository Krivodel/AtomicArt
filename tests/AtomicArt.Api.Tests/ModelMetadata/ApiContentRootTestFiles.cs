using System.Text;

using AtomicArt.Infrastructure.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.ModelMetadata;

internal static class ApiContentRootTestFiles
{
    public static void CopyModelMetadata(string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);

        string sourceMetadataPath = ApiModelMetadataTestCatalog.GetMetadataPath();
        WriteModelMetadataFile(
            contentRoot,
            metadataPath => File.Copy(sourceMetadataPath, metadataPath));
    }

    public static void WriteAppSettings(string contentRoot, string appSettingsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(appSettingsJson);

        File.WriteAllText(
            Path.Combine(contentRoot, "appsettings.json"),
            appSettingsJson,
            Encoding.UTF8);
    }

    public static void WriteModelMetadata(string contentRoot, string metadataJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataJson);

        WriteModelMetadataFile(
            contentRoot,
            metadataPath => File.WriteAllText(metadataPath, metadataJson, Encoding.UTF8));
    }

    private static void WriteModelMetadataFile(
        string contentRoot,
        Action<string> writeFile)
    {
        string metadataPath = Path.Combine(
            contentRoot,
            GenerationModelCatalogDefaults.RelativePath);
        string metadataDirectory = Path.GetDirectoryName(metadataPath)
            ?? throw new InvalidOperationException("Model metadata directory was not found.");

        Directory.CreateDirectory(metadataDirectory);
        writeFile(metadataPath);
    }
}
