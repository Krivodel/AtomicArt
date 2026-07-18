using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services;

public sealed class TrustedImageFileService : ITrustedImageFileService
{
    private const string InvalidImagePathMessage = "Image file path is not trusted.";
    private const string TrustedPathFailureMessage =
        "Trusted image directories must stay inside AtomicArt data root and must not contain reparse points.";
    private const long MaxTrustedImageBytes = GenerationImageContentValidator.DefaultMaxImageBytes;
    private const int SignatureReadBytes = 64;

    private readonly ILogger<TrustedImageFileService> _logger;
    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly string _trustedRootDirectory;
    private readonly string[] _trustedDirectories;

    public TrustedImageFileService(
        IAtomicArtDataPathProvider pathProvider,
        IGenerationImageFormatRegistry formatRegistry,
        ILogger<TrustedImageFileService> logger)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(formatRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _formatRegistry = formatRegistry;
        _logger = logger;
        _pathProvider = pathProvider;
        _trustedRootDirectory = Path.GetFullPath(pathProvider.RootDirectory);
        _trustedDirectories =
        [
            Path.GetFullPath(pathProvider.ArtDirectory),
            Path.GetFullPath(pathProvider.ThumbnailsDirectory)
        ];
        EnsureTrustedDirectories();
    }

    public string? GetTrustedImagePathOrDefault(string? path, string modelId)
    {
        if (TryGetTrustedImagePath(path, modelId, out string? trustedPath))
        {
            return trustedPath;
        }

        return null;
    }

    public string GetTrustedImagePath(string? path, string modelId)
    {
        if (TryGetTrustedImagePath(path, modelId, out string? trustedPath)
            && trustedPath is not null)
        {
            return trustedPath;
        }

        throw new InvalidOperationException(InvalidImagePathMessage);
    }

    public void DeleteTrustedImageFileIfExists(
        string? path,
        string modelId,
        Action<string> validateResolvedPath)
    {
        ArgumentNullException.ThrowIfNull(validateResolvedPath);

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException(InvalidImagePathMessage);
        }

        TrustedPathGuard.DeleteTrustedFileIfExists(
            path,
            _trustedDirectories,
            _trustedRootDirectory,
            validateResolvedPath);
    }

    private bool TryGetTrustedImagePath(string? path, string modelId, out string? trustedPath)
    {
        trustedPath = null;

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        return TryValidatePath(path, out trustedPath);
    }

    private bool TryValidatePath(
        string path,
        out string? trustedPath)
    {
        trustedPath = null;

        try
        {
            if (!TrustedPathGuard.TryOpenTrustedExistingFileForRead(
                path,
                _trustedDirectories,
                _trustedRootDirectory,
                TrustedPathFailureMessage,
                out FileStream? stream,
                out string? trustedFullPath)
                || stream is null
                || trustedFullPath is null)
            {
                return false;
            }

            using (stream)
            {
                FileInfo fileInfo = new(trustedFullPath);

                if (stream.Length <= 0
                    || stream.Length > MaxTrustedImageBytes)
                {
                    return false;
                }

                if (!_formatRegistry.TryGetByFileName(
                    fileInfo.Name,
                    out IGenerationImageFormat? format)
                    || format is null)
                {
                    return false;
                }

                byte[] signatureBytes = ReadSignatureBytes(stream);

                if (!format.MatchesSignature(signatureBytes))
                {
                    return false;
                }

                trustedPath = trustedFullPath;
                return true;
            }
        }
        catch (ArgumentException ex)
        {
            LogTrustedImageValidationFailure(ex);

            return false;
        }
        catch (PathTooLongException ex)
        {
            LogTrustedImageValidationFailure(ex);

            return false;
        }
        catch (IOException ex)
        {
            LogTrustedImageValidationFailure(ex);

            return false;
        }
        catch (NotSupportedException ex)
        {
            LogTrustedImageValidationFailure(ex);

            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogTrustedImageValidationFailure(ex);

            return false;
        }
    }

    private static byte[] ReadSignatureBytes(FileStream stream)
    {
        byte[] buffer = new byte[Math.Min(SignatureReadBytes, (int)stream.Length)];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);

        if (bytesRead == buffer.Length)
        {
            return buffer;
        }

        byte[] result = new byte[bytesRead];
        Array.Copy(buffer, result, bytesRead);

        return result;
    }

    private void LogTrustedImageValidationFailure(Exception exception)
    {
        _logger.LogWarning(exception, "Failed to validate trusted image file.");
    }

    private void EnsureTrustedDirectories()
    {
        foreach (string trustedDirectory in _trustedDirectories)
        {
            TrustedPathGuard.EnsureTrustedDirectoryExists(
                _pathProvider,
                trustedDirectory,
                TrustedPathFailureMessage);
        }
    }
}
