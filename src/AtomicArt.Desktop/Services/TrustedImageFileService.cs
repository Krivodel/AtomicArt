using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services;

public sealed class TrustedImageFileService : ITrustedImageFileService
{
    private const string InvalidImagePathMessage = "Image file path is not trusted.";
    private const int SignatureReadBytes = 64;

    private static readonly string TrustedPathFailureMessage =
        TrustedPathGuard.CreateFailureMessage(
            "Trusted image directories",
            "AtomicArt data root");
    private readonly ILogger<TrustedImageFileService> _logger;
    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly long _maxTrustedImageBytes;
    private readonly TrustedFileStreamFactory _trustedFileStreamFactory;

    public TrustedImageFileService(
        IAtomicArtDataPathProvider pathProvider,
        IGenerationImageFormatRegistry formatRegistry,
        ILogger<TrustedImageFileService> logger,
        IOptions<GenerationClientOptions> options,
        TrustedFileStreamFactory trustedFileStreamFactory)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(formatRegistry);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(trustedFileStreamFactory);

        _formatRegistry = formatRegistry;
        _logger = logger;
        _pathProvider = pathProvider;
        _maxTrustedImageBytes = options.Value.MaxInputImageBytes;
        _trustedFileStreamFactory = trustedFileStreamFactory;
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
        string trustedRootDirectory = Path.GetFullPath(_pathProvider.RootDirectory);
        string[] trustedDirectories = GetTrustedDirectories();

        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException(InvalidImagePathMessage);
        }

        TrustedPathGuard.DeleteTrustedFileIfExists(
            path,
            trustedDirectories,
            trustedRootDirectory,
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
            string trustedRootDirectory = Path.GetFullPath(_pathProvider.RootDirectory);
            string[] trustedDirectories = GetTrustedDirectories();

            if (!_trustedFileStreamFactory.TryOpenExistingFileForRead(
                path,
                trustedDirectories,
                trustedRootDirectory,
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
                    || stream.Length > _maxTrustedImageBytes)
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
        foreach (string trustedDirectory in GetTrustedDirectories())
        {
            TrustedPathGuard.EnsureTrustedDirectoryExists(
                _pathProvider,
                trustedDirectory,
                TrustedPathFailureMessage);
        }
    }

    private string[] GetTrustedDirectories()
    {
        return
        [
            Path.GetFullPath(_pathProvider.ArtDirectory),
            Path.GetFullPath(_pathProvider.ThumbnailsDirectory)
        ];
    }
}
