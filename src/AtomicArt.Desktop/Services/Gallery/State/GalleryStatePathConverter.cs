using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryStatePathConverter
{
    private const char PersistedDirectorySeparator = '/';

    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly ITrustedImageFileService _trustedImageFileService;
    private readonly ILogger<GalleryStatePathConverter> _logger;

    public GalleryStatePathConverter(
        IAtomicArtDataPathProvider pathProvider,
        ITrustedImageFileService trustedImageFileService,
        ILogger<GalleryStatePathConverter> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _trustedImageFileService = trustedImageFileService
            ?? throw new ArgumentNullException(nameof(trustedImageFileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? GetRuntimeImagePath(string? storedPath, string modelId)
    {
        return GetRuntimePath(storedPath, modelId, _pathProvider.ArtDirectory);
    }

    public string? GetRuntimeThumbnailPath(string? storedPath, string modelId)
    {
        return GetRuntimePath(storedPath, modelId, _pathProvider.ThumbnailsDirectory);
    }

    public string? GetValidatedRuntimePath(string? runtimePath, string modelId)
    {
        return _trustedImageFileService.GetTrustedImagePathOrDefault(
            runtimePath,
            modelId);
    }

    public string? GetStoragePath(string? validatedRuntimePath)
    {
        string? fullPath = GetFullPathOrDefault(validatedRuntimePath);
        string rootDirectory = Path.GetFullPath(_pathProvider.RootDirectory);

        if (fullPath is null
            || !TrustedPathGuard.IsInsideDirectory(rootDirectory, fullPath))
        {
            return null;
        }

        string relativePath = Path.GetRelativePath(rootDirectory, fullPath);

        if (Path.IsPathRooted(relativePath))
        {
            return null;
        }

        return relativePath.Replace(
            Path.DirectorySeparatorChar,
            PersistedDirectorySeparator);
    }

    private static string? GetRelocatedLegacyPathOrDefault(
        string storedPath,
        string targetDirectory)
    {
        string? storedDirectory = Path.GetDirectoryName(storedPath);

        if (string.IsNullOrWhiteSpace(storedDirectory)
            || !string.Equals(
                Path.GetFileName(storedDirectory),
                Path.GetFileName(targetDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string fileName = Path.GetFileName(storedPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.GetFullPath(Path.Combine(targetDirectory, fileName));
    }

    private static string NormalizeDirectorySeparators(string path)
    {
        return path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private string? GetRuntimePath(
        string? storedPath,
        string modelId,
        string legacyTargetDirectory)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        string? absolutePath = GetAbsoluteStoredPathOrDefault(storedPath);

        if (absolutePath is null)
        {
            return null;
        }

        string? trustedPath = _trustedImageFileService.GetTrustedImagePathOrDefault(
            absolutePath,
            modelId);

        if (trustedPath is not null || !Path.IsPathFullyQualified(storedPath))
        {
            return trustedPath;
        }

        string? relocatedPath = GetRelocatedLegacyPathOrDefault(
            storedPath,
            legacyTargetDirectory);

        return _trustedImageFileService.GetTrustedImagePathOrDefault(
            relocatedPath,
            modelId);
    }

    private string? GetAbsoluteStoredPathOrDefault(string storedPath)
    {
        if (Path.IsPathFullyQualified(storedPath))
        {
            return GetFullPathOrDefault(storedPath);
        }

        if (Path.IsPathRooted(storedPath))
        {
            return null;
        }

        string platformPath = NormalizeDirectorySeparators(storedPath);

        return GetFullPathOrDefault(Path.Combine(
            _pathProvider.RootDirectory,
            platformPath));
    }

    private string? GetFullPathOrDefault(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException ex)
        {
            LogPathConversionFailure(ex);

            return null;
        }
        catch (IOException ex)
        {
            LogPathConversionFailure(ex);

            return null;
        }
        catch (NotSupportedException ex)
        {
            LogPathConversionFailure(ex);

            return null;
        }
    }

    private void LogPathConversionFailure(Exception exception)
    {
        _logger.LogWarning(exception, "Failed to convert a gallery state file path.");
    }
}
