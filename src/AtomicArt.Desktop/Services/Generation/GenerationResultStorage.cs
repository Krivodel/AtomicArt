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
    private readonly string _resultsDirectory;

    public GenerationResultStorage(
        IAtomicArtDataPathProvider pathProvider,
        IGenerationImageFormatRegistry formatRegistry,
        GenerationImageFileNamePolicy fileNamePolicy,
        ILogger<GenerationResultStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(formatRegistry);
        ArgumentNullException.ThrowIfNull(fileNamePolicy);
        ArgumentNullException.ThrowIfNull(logger);

        _formatRegistry = formatRegistry;
        _fileNamePolicy = fileNamePolicy;
        _logger = logger;
        _pathProvider = pathProvider;
        _resultsDirectory = Path.GetFullPath(pathProvider.ArtDirectory);
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
            string? resultPath = GetExpectedResultPathOrDefault(batchId, itemId, content.ContentType);

            if (resultPath is null)
            {
                throw new ArgumentException("Generation result path could not be built.", nameof(content));
            }

            TrustedPathGuard.EnsureTrustedDirectoryExists(
                _pathProvider,
                _resultsDirectory,
                TrustedPathFailureMessage);
            TrustedPathGuard.EnsureTrustedWriteTarget(
                _resultsDirectory,
                resultPath,
                TrustedPathFailureMessage);
            await WriteVerifiedResultFileAsync(resultPath, content.Bytes, ct).ConfigureAwait(false);
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
        string resultPath = Path.GetFullPath(Path.Combine(_resultsDirectory, fileName));

        if (!TrustedPathGuard.IsInsideDirectory(_resultsDirectory, resultPath))
        {
            return null;
        }

        return resultPath;
    }

    private async Task WriteVerifiedResultFileAsync(
        string resultPath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken ct)
    {
        await using FileStream stream = TrustedPathGuard.CreateTrustedNewFileForWrite(
            _resultsDirectory,
            resultPath,
            TrustedPathFailureMessage);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }
}
