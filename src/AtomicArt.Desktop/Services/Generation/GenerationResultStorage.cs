using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

public sealed class GenerationResultStorage : IGenerationResultStorage
{
    private static readonly string TrustedPathFailureMessage =
        TrustedPathGuard.CreateFailureMessage(
            "Generation result path",
            AtomicArtPathNames.ArtDirectory);
    private readonly ILogger<GenerationResultStorage> _logger;
    private readonly IGenerationImageFormatRegistry _formatRegistry;
    private readonly GenerationImageFileNamePolicy _fileNamePolicy;
    private readonly IAtomicArtDataPathProvider _pathProvider;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly TrustedFileStreamFactory _trustedFileStreamFactory;

    public GenerationResultStorage(
        IAtomicArtDataPathProvider pathProvider,
        IGenerationImageFormatRegistry formatRegistry,
        GenerationImageFileNamePolicy fileNamePolicy,
        IDataRootAccessCoordinator accessCoordinator,
        TrustedFileStreamFactory trustedFileStreamFactory,
        ILogger<GenerationResultStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(formatRegistry);
        ArgumentNullException.ThrowIfNull(fileNamePolicy);
        ArgumentNullException.ThrowIfNull(accessCoordinator);
        ArgumentNullException.ThrowIfNull(trustedFileStreamFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _formatRegistry = formatRegistry;
        _fileNamePolicy = fileNamePolicy;
        _logger = logger;
        _pathProvider = pathProvider;
        _accessCoordinator = accessCoordinator;
        _trustedFileStreamFactory = trustedFileStreamFactory;
    }

    public async Task SaveAsync(
        Guid batchId,
        Guid itemId,
        GenerationImageContentValidationResult content,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            using DataRootAccessLease accessLease =
                await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);
            string resultsDirectory = Path.GetFullPath(_pathProvider.ArtDirectory);
            string? resultPath = GetExpectedResultPathOrDefault(
                resultsDirectory,
                batchId,
                itemId,
                content.ContentType);

            if (resultPath is null)
            {
                throw new ArgumentException("Generation result path could not be built.", nameof(content));
            }

            TrustedPathGuard.EnsureTrustedDirectoryExists(
                _pathProvider,
                resultsDirectory,
                TrustedPathFailureMessage);
            TrustedPathGuard.EnsureTrustedWriteTarget(
                resultsDirectory,
                resultPath,
                TrustedPathFailureMessage);
            await WriteVerifiedResultFileAsync(
                resultsDirectory,
                resultPath,
                content.Bytes,
                ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Generation result path is invalid for batch {BatchId} item {ItemId}",
                batchId,
                itemId);

            throw;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to write generation result for batch {BatchId} item {ItemId}",
                batchId,
                itemId);

            throw;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(
                ex,
                "Generation result path is not supported for batch {BatchId} item {ItemId}",
                batchId,
                itemId);

            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Generation result write is not authorized for batch {BatchId} item {ItemId}",
                batchId,
                itemId);
            throw;
        }
    }

    public string? GetExpectedResultPathOrDefault(
        Guid batchId,
        Guid itemId,
        string contentType)
    {
        string resultsDirectory = Path.GetFullPath(_pathProvider.ArtDirectory);

        return GetExpectedResultPathOrDefault(
            resultsDirectory,
            batchId,
            itemId,
            contentType);
    }

    private string? GetExpectedResultPathOrDefault(
        string resultsDirectory,
        Guid batchId,
        Guid itemId,
        string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (batchId == Guid.Empty
            || itemId == Guid.Empty
            || !_formatRegistry.TryGetByContentType(
                contentType,
                out IGenerationImageFormat? format)
            || format is null)
        {
            return null;
        }

        string fileName = _fileNamePolicy.BuildFileName(batchId, itemId, format.Extension);
        string resultPath = Path.GetFullPath(Path.Combine(resultsDirectory, fileName));

        if (!TrustedPathGuard.IsInsideDirectory(resultsDirectory, resultPath))
        {
            return null;
        }

        return resultPath;
    }

    private async Task WriteVerifiedResultFileAsync(
        string resultsDirectory,
        string resultPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken ct)
    {
        await using FileStream stream = _trustedFileStreamFactory.CreateNewFileForWrite(
            resultsDirectory,
            resultPath,
            TrustedPathFailureMessage);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }
}
