using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

public sealed class GalleryThumbnailStorage : IGalleryThumbnailStorage
{
    private const string TemporaryThumbnailDeleteFailureMessage =
        "Failed to delete temporary gallery thumbnail file.";

    private static readonly string TrustedPathFailureMessage =
        TrustedPathGuard.CreateFailureMessage("Gallery thumbnail path", "Thumbnails");
    private readonly ILogger<GalleryThumbnailStorage> _logger;
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly ITrustedImageFileService _trustedImageFileService;
    private readonly GenerationImageFileNamePolicy _fileNamePolicy;
    private readonly GalleryThumbnailImageFormat _thumbnailImageFormat;
    private readonly IGalleryThumbnailGenerator _thumbnailGenerator;
    private readonly string _thumbnailsDirectory;

    public GalleryThumbnailStorage(
        IAtomicArtDataPathProvider pathProvider,
        ITrustedImageFileService trustedImageFileService,
        GenerationImageFileNamePolicy fileNamePolicy,
        GalleryThumbnailImageFormat thumbnailImageFormat,
        IGalleryThumbnailGenerator thumbnailGenerator,
        ILogger<GalleryThumbnailStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(trustedImageFileService);
        ArgumentNullException.ThrowIfNull(fileNamePolicy);
        ArgumentNullException.ThrowIfNull(thumbnailImageFormat);
        ArgumentNullException.ThrowIfNull(thumbnailGenerator);
        ArgumentNullException.ThrowIfNull(logger);

        _pathProvider = pathProvider;
        _trustedImageFileService = trustedImageFileService;
        _fileNamePolicy = fileNamePolicy;
        _thumbnailImageFormat = thumbnailImageFormat;
        _thumbnailGenerator = thumbnailGenerator;
        _logger = logger;
        _thumbnailsDirectory = Path.GetFullPath(pathProvider.ThumbnailsDirectory);
    }

    public string? GetThumbnailPathOrDefault(
        Guid batchId,
        Guid itemId,
        string modelId)
    {
        try
        {
            string thumbnailPath = BuildThumbnailPath(batchId, itemId);

            return _trustedImageFileService.GetTrustedImagePathOrDefault(
                thumbnailPath,
                modelId);
        }
        catch (ArgumentException ex)
        {
            LogThumbnailPathResolutionFailure(ex, batchId, itemId);

            return null;
        }
        catch (IOException ex)
        {
            LogThumbnailPathResolutionFailure(ex, batchId, itemId);

            return null;
        }
        catch (NotSupportedException ex)
        {
            LogThumbnailPathResolutionFailure(ex, batchId, itemId);

            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogThumbnailPathResolutionFailure(ex, batchId, itemId);

            return null;
        }
    }

    public async Task SaveAsync(
        Guid batchId,
        Guid itemId,
        string modelId,
        string? fullImagePath,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fullImagePath))
        {
            LogUntrustedFullImagePath(batchId, itemId);
            return;
        }

        string? trustedFullImagePath = _trustedImageFileService.GetTrustedImagePathOrDefault(
            fullImagePath,
            modelId);

        if (trustedFullImagePath is null)
        {
            LogUntrustedFullImagePath(batchId, itemId);
            return;
        }

        try
        {
            byte[] thumbnailBytes = await _thumbnailGenerator
                .CreateThumbnailAsync(trustedFullImagePath, ct)
                .ConfigureAwait(false);
            string thumbnailPath = GetTrustedThumbnailWritePath(batchId, itemId);

            await WriteThumbnailAsync(thumbnailPath, thumbnailBytes, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Saved gallery thumbnail for batch {BatchId} item {ItemId} with {ByteCount} bytes",
                batchId,
                itemId,
                thumbnailBytes.Length);

            if (GetThumbnailPathOrDefault(batchId, itemId, modelId) is null)
            {
                _logger.LogWarning(
                    "Saved gallery thumbnail path is not trusted for batch {BatchId} item {ItemId}",
                    batchId,
                    itemId);
            }
        }
        catch (ArgumentException ex)
        {
            LogThumbnailCreationFailure(ex, batchId, itemId);
            throw;
        }
        catch (InvalidDataException ex)
        {
            LogThumbnailCreationFailure(ex, batchId, itemId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            LogThumbnailCreationFailure(ex, batchId, itemId);
            throw;
        }
        catch (IOException ex)
        {
            LogThumbnailCreationFailure(ex, batchId, itemId);
            throw;
        }
        catch (NotSupportedException ex)
        {
            LogThumbnailCreationFailure(ex, batchId, itemId);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogThumbnailCreationFailure(ex, batchId, itemId);
            throw;
        }
    }

    private string BuildThumbnailPath(Guid batchId, Guid itemId)
    {
        string fileName = _fileNamePolicy.BuildFileName(
            batchId,
            itemId,
            _thumbnailImageFormat.Extension);
        string thumbnailPath = Path.GetFullPath(Path.Combine(_thumbnailsDirectory, fileName));

        TrustedPathGuard.EnsureInsideDirectory(
            _thumbnailsDirectory,
            thumbnailPath,
            TrustedPathFailureMessage);

        return thumbnailPath;
    }

    private string GetTrustedThumbnailWritePath(Guid batchId, Guid itemId)
    {
        string thumbnailPath = BuildThumbnailPath(batchId, itemId);

        TrustedPathGuard.EnsureTrustedDirectoryExists(
            _pathProvider,
            _thumbnailsDirectory,
            TrustedPathFailureMessage);
        TrustedPathGuard.EnsureTrustedWriteTarget(
            _thumbnailsDirectory,
            thumbnailPath,
            TrustedPathFailureMessage);

        return thumbnailPath;
    }

    private async Task WriteThumbnailAsync(
        string thumbnailPath,
        ReadOnlyMemory<byte> thumbnailBytes,
        CancellationToken ct)
    {
        string tempPath = AtomicFileWriteTempPath.CreateSibling(
            _thumbnailsDirectory,
            Path.GetFileName(thumbnailPath));

        try
        {
            await using (FileStream stream = TrustedPathGuard.CreateTrustedNewFileForWrite(
                _thumbnailsDirectory,
                tempPath,
                TrustedPathFailureMessage))
            {
                await stream.WriteAsync(thumbnailBytes, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            TrustedPathGuard.ReplaceTrustedFile(
                _thumbnailsDirectory,
                tempPath,
                thumbnailPath,
                TrustedPathFailureMessage);
        }
        finally
        {
            DeleteTempFileIfExists(tempPath);
        }
    }

    private void LogUntrustedFullImagePath(Guid batchId, Guid itemId)
    {
        _logger.LogWarning(
            "Gallery thumbnail source image path is not trusted for batch {BatchId} item {ItemId}",
            batchId,
            itemId);
    }

    private void LogThumbnailCreationFailure(Exception exception, Guid batchId, Guid itemId)
    {
        _logger.LogWarning(
            exception,
            "Failed to create gallery thumbnail for batch {BatchId} item {ItemId}",
            batchId,
            itemId);
    }

    private void LogThumbnailPathResolutionFailure(Exception exception, Guid batchId, Guid itemId)
    {
        _logger.LogWarning(
            exception,
            "Failed to resolve gallery thumbnail path for batch {BatchId} item {ItemId}",
            batchId,
            itemId);
    }

    private void DeleteTempFileIfExists(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, TemporaryThumbnailDeleteFailureMessage);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, TemporaryThumbnailDeleteFailureMessage);
        }
    }
}
