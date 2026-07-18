using System.Reflection;

namespace AtomicArt.Infrastructure.Generation;

public static class GenerationModelCatalogDefaults
{
    public static string FileName => GetRelativePathParts().Last();
    public static string RelativePath => GetRequiredAssemblyMetadata(MetadataPathMetadataKey);
    public static string SourceProjectName => typeof(GenerationModelCatalogDefaults).Assembly.GetName().Name
        ?? throw new InvalidOperationException("Infrastructure assembly name is missing.");

    private const string MetadataPathMetadataKey = "GenerationModelCatalogMetadataPath";
    private const string SourceRootDirectory = "src";

    public static string ResolvePath(string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        string contentRootCandidate = Path.Combine(
            [contentRootPath, .. GetRelativePathParts()]);

        if (File.Exists(contentRootCandidate))
        {
            return contentRootCandidate;
        }

        string? sourceCandidate = FindSourceCatalogPath(contentRootPath);

        return sourceCandidate ?? contentRootCandidate;
    }

    private static string? FindSourceCatalogPath(string contentRootPath)
    {
        DirectoryInfo? directory = new(contentRootPath);

        while (directory is not null)
        {
            string candidatePath = Path.Combine(
                [
                directory.FullName,
                SourceRootDirectory,
                SourceProjectName,
                .. GetRelativePathParts()
                ]);

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string GetRequiredAssemblyMetadata(string key)
    {
        string? value = typeof(GenerationModelCatalogDefaults)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Infrastructure assembly metadata '{key}' is missing.");
        }

        return value;
    }

    private static string[] GetRelativePathParts()
    {
        return RelativePath
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\\', '/'],
                StringSplitOptions.RemoveEmptyEntries);
    }
}
