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

    public string? GetImagePathForDeletion(string? storedPath)
    {
        return GetManagedPathForDeletion(
            storedPath,
            _pathProvider.ArtDirectory);
    }

    public string? GetThumbnailPathForDeletion(string? storedPath)
    {
        return GetManagedPathForDeletion(
            storedPath,
            _pathProvider.ThumbnailsDirectory);
    }

    public bool IsStoredImageFileMissing(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return true;
        }

        string? managedPath = GetImagePathForDeletion(storedPath);

        return managedPath is not null && !File.Exists(managedPath);
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
        return ResolveStoredPathOrDefault(
            storedPath,
            legacyTargetDirectory,
            path => _trustedImageFileService.GetTrustedImagePathOrDefault(
                path,
                modelId));
    }

    private string? GetManagedPathForDeletion(
        string? storedPath,
        string targetDirectory)
    {
        return ResolveStoredPathOrDefault(
            storedPath,
            targetDirectory,
            path => GetTrustedDeletionPathOrDefault(path, targetDirectory));
    }

    private string? ResolveStoredPathOrDefault(
        string? storedPath,
        string targetDirectory,
        Func<string?, string?> resolvePath)
    {
        ArgumentNullException.ThrowIfNull(resolvePath);

        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        string? absolutePath = GetAbsoluteStoredPathOrDefault(storedPath);

        if (absolutePath is null)
        {
            return null;
        }

        string? trustedPath = resolvePath(absolutePath);

        if (trustedPath is not null || !Path.IsPathFullyQualified(storedPath))
        {
            return trustedPath;
        }

        string? relocatedPath = GetRelocatedLegacyPathOrDefault(
            storedPath,
            targetDirectory);

        return resolvePath(relocatedPath);
    }

    private string? GetTrustedDeletionPathOrDefault(
        string? path,
        string trustedDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string trustedRootDirectory = Path.GetFullPath(_pathProvider.RootDirectory);
            string[] trustedDirectories = [Path.GetFullPath(trustedDirectory)];

            return TrustedPathGuard.TryResolveTrustedPathForDeletion(
                path,
                trustedDirectories,
                trustedRootDirectory,
                out string? trustedPath)
                ? trustedPath
                : null;
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
        catch (UnauthorizedAccessException ex)
        {
            LogPathConversionFailure(ex);

            return null;
        }
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
