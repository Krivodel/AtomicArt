using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Gallery.Deletion;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryImageDeduplicationService
{
    private const string UnsafeImageMessage = "The duplicate gallery image path is unsafe.";

    private readonly IGenerationReferenceStore _references;
    private readonly IAtomicArtDataPathProvider _paths;
    private readonly IDataRootAccessCoordinator _access;
    private readonly TrustedFileStreamFactory _streams;
    private readonly GenerationImageFileNamePolicy _fileNames;
    private readonly IGalleryItemDeletionService _deletion;
    private readonly ILogger<GalleryImageDeduplicationService> _logger;

    public GalleryImageDeduplicationService(
        IGenerationReferenceStore references, IAtomicArtDataPathProvider paths,
        IDataRootAccessCoordinator access, TrustedFileStreamFactory streams,
        GenerationImageFileNamePolicy fileNames, IGalleryItemDeletionService deletion,
        ILogger<GalleryImageDeduplicationService> logger)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _streams = streams ?? throw new ArgumentNullException(nameof(streams));
        _fileNames = fileNames ?? throw new ArgumentNullException(nameof(fileNames));
        _deletion = deletion ?? throw new ArgumentNullException(nameof(deletion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<GalleryImageDuplicate>> FindAsync(
        IReadOnlyList<GalleryItemState> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        List<GalleryImageDuplicate> duplicates = [];

        foreach (GalleryItemState item in items)
        {
            ct.ThrowIfCancellationRequested();

            if (item.ChatResult?.Image is not { } reference || item.ImagePath is not { } path
                || !_fileNames.IsFileNameForItem(Path.GetFileName(path), item.Id))
            {
                continue;
            }

            try
            {
                string? sharedPath = await _references.GetExistingFilePathAsync(reference, ct).ConfigureAwait(false);
                string? sharedHash = await _references.ReadAsync<string>(reference,
                    GenerationReferenceFilePolicy.HashAsync, ct).ConfigureAwait(false);

                if (sharedPath is null || !string.Equals(sharedHash, reference.Hash, StringComparison.Ordinal))
                {
                    continue;
                }

                using DataRootAccessLease lease = await _access.AcquireAccessAsync(ct).ConfigureAwait(false);

                if (!_streams.TryOpenExistingFileForRead(path, new string[] { _paths.ArtDirectory },
                    _paths.RootDirectory, UnsafeImageMessage, out FileStream? stream, out string? trustedPath)
                    || stream is null || trustedPath is null)
                {
                    stream?.Dispose();
                    continue;
                }

                await using (stream.ConfigureAwait(false))
                {
                    string? originalHash = await GenerationReferenceFilePolicy.HashAsync(stream, ct).ConfigureAwait(false);

                    if (string.Equals(originalHash, sharedHash, StringComparison.Ordinal))
                    {
                        duplicates.Add(new GalleryImageDuplicate(item.Id, item.ModelId, trustedPath, sharedPath));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not compare stored images for gallery item {ItemId}.", item.Id);
            }
        }

        return duplicates;
    }

    public Task DeleteAsync(IReadOnlyList<GalleryImageDuplicate> duplicates, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(duplicates);
        GalleryItemDeletionRequest[] requests = duplicates.Select(duplicate => new GalleryItemDeletionRequest(
            duplicate.ItemId, duplicate.ModelId, duplicate.OriginalPath, null)).ToArray();

        return _deletion.DeleteFilesAsync(requests, ct);
    }

}
