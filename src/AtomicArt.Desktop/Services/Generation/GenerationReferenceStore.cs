using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed partial class GenerationReferenceStore : IGenerationReferenceStore
{
    private const string TrustedPathFailureMessage = "The generation reference path is unsafe.";

    private readonly IAtomicArtDataPathProvider _paths;
    private readonly IDataRootAccessCoordinator _accessCoordinator;
    private readonly TrustedFileStreamFactory _streams;
    private readonly IGenerationImageFormatRegistry _formats;
    private readonly ILogger<GenerationReferenceStore> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _pendingLock = new();
    private readonly Dictionary<Guid, HashSet<string>> _pendingHashes = [];

    public GenerationReferenceStore(
        IAtomicArtDataPathProvider paths,
        IDataRootAccessCoordinator accessCoordinator,
        TrustedFileStreamFactory streams,
        IGenerationImageFormatRegistry formats,
        ILogger<GenerationReferenceStore> logger)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _accessCoordinator = accessCoordinator ?? throw new ArgumentNullException(nameof(accessCoordinator));
        _streams = streams ?? throw new ArgumentNullException(nameof(streams));
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<GenerationReference>> SaveAsync(
        Guid runId,
        IReadOnlyList<AttachedImageDto> images,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(images);
        ct.ThrowIfCancellationRequested();

        if (images.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<GenerationReference>>(Array.Empty<GenerationReference>());
        }

        return Task.Run(() => SaveCoreAsync(runId, images, ct), ct);
    }

    public Task<AttachedImageDto?> LoadAsync(GenerationReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return ReadAsync<AttachedImageDto>(
            reference,
            (stream, token) => ReadImageAsync(reference, stream, token),
            ct);
    }

    public async Task SaveOwnedAsync(string relativeOwnerPath, AttachedImageDto image, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeOwnerPath);
        ArgumentNullException.ThrowIfNull(image);

        if (Path.IsPathRooted(relativeOwnerPath)
            || !SafeFileName.IsValid(Path.GetDirectoryName(relativeOwnerPath) ?? string.Empty)
            || !SafeFileName.IsValid(Path.GetFileName(relativeOwnerPath))
            || !relativeOwnerPath.EndsWith(GenerationReferenceFilePolicy.OwnerFileExtension, StringComparison.Ordinal))
        {
            throw new IOException("The shared image owner path is unsafe.");
        }

        Guid leaseId = Guid.NewGuid();

        try
        {
            IReadOnlyList<GenerationReference> saved = await SaveAsync(leaseId, new AttachedImageDto[] { image }, ct)
                .ConfigureAwait(false);
            await _writeLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                using DataRootAccessLease access = await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);
                string ownerPath = Path.Combine(_paths.StateAttachmentsDirectory, relativeOwnerPath);
                string directory = Path.GetDirectoryName(ownerPath)
                    ?? throw new IOException(TrustedPathFailureMessage);
                TrustedPathGuard.EnsureTrustedDirectoryExists(directory,
                    path => Directory.CreateDirectory(path), TrustedPathFailureMessage);
                string tempPath = AtomicFileWriteTempPath.CreateSibling(directory, Path.GetFileName(ownerPath));

                try
                {
                    await using (FileStream stream = _streams.CreateNewFileForWrite(directory, tempPath, TrustedPathFailureMessage))
                    {
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(saved.Single().Hash), ct).ConfigureAwait(false);
                        await stream.FlushAsync(ct).ConfigureAwait(false);
                    }

                    TrustedPathGuard.ReplaceTrustedFile(directory, tempPath, ownerPath, TrustedPathFailureMessage);
                }
                finally
                {
                    FileDeletion.DeleteIfExists(tempPath);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }
        finally
        {
            Release(leaseId);
        }
    }

    public string? ResolveExistingFilePath(GenerationReference reference)
    {
        using FileStream? stream = OpenTrustedFile(reference, out string? trustedPath);

        return stream is null ? null : trustedPath;
    }

    public async Task<string?> GetExistingFilePathAsync(GenerationReference reference, CancellationToken ct)
    {
        using DataRootAccessLease lease = await _accessCoordinator.AcquireAccessAsync(ct)
            .ConfigureAwait(false);

        return ResolveExistingFilePath(reference);
    }

    public async Task<TResult?> ReadAsync<TResult>(
        GenerationReference reference,
        Func<Stream, CancellationToken, Task<TResult?>> read,
        CancellationToken ct) where TResult : class
    {
        ArgumentNullException.ThrowIfNull(read);
        Guid readLeaseId = Guid.NewGuid();

        try
        {
            await RetainReadPathsAsync(readLeaseId, new GenerationReference[] { reference }, ct).ConfigureAwait(false);
            using DataRootAccessLease lease = await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);
            FileStream? stream = OpenTrustedFile(reference, out _);

            if (stream is null)
            {
                return null;
            }

            await using (stream.ConfigureAwait(false))
            {
                return await read(stream, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            Release(readLeaseId);
        }
    }

    public void Release(Guid runId)
    {
        ReleaseCore(runId);
    }

    public async Task RetainAsync(Guid runId, IEnumerable<GenerationReference> references, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(references);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            Retain(runId, references);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task PruneAsync(IReadOnlySet<string> retainedHashes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retainedHashes);

        return SynchronizeCoreAsync(null, retainedHashes, ct);
    }

    private static async Task<AttachedImageDto?> ReadImageAsync(
        GenerationReference reference,
        Stream stream,
        CancellationToken ct)
    {
        if (stream.Length <= 0 || stream.Length > Array.MaxLength)
        {
            return null;
        }

        byte[] content = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(content, ct).ConfigureAwait(false);

        if (!string.Equals(reference.Hash, Convert.ToHexStringLower(SHA256.HashData(content)), StringComparison.Ordinal))
        {
            return null;
        }

        return new AttachedImageDto(reference.FileName, reference.ContentType, content);
    }

    private async Task<IReadOnlyList<GenerationReference>> SaveCoreAsync(
        Guid runId,
        IReadOnlyList<AttachedImageDto> images,
        CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            using DataRootAccessLease lease = await _accessCoordinator.AcquireAccessAsync(ct)
                .ConfigureAwait(false);
            GenerationReference[] references = new GenerationReference[images.Count];

            for (int index = 0; index < images.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                AttachedImageDto image = images[index];
                string hash = Convert.ToHexStringLower(SHA256.HashData(image.Content));
                GenerationReference reference = new(hash, image.FileName, image.ContentType);
                string historyPath = GetPath(GetDirectory(), reference)
                    ?? throw new InvalidDataException("The generation reference format is unsupported.");
                string artPath = Path.Combine(_paths.ArtDirectory, Path.GetFileName(historyPath));
                string path = File.Exists(artPath) ? artPath : historyPath;
                string directory = Path.GetDirectoryName(path)
                    ?? throw new IOException(TrustedPathFailureMessage);
                TrustedPathGuard.EnsureTrustedDirectoryExists(directory,
                    target => Directory.CreateDirectory(target), TrustedPathFailureMessage);
                await WriteAsync(directory, path, image.Content, ct).ConfigureAwait(false);
                _dirtyHashes.Add(hash);
                references[index] = reference;
            }

            Retain(runId, references);

            return references;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private FileStream? OpenTrustedFile(GenerationReference reference, out string? trustedPath)
    {
        ArgumentNullException.ThrowIfNull(reference);
        trustedPath = null;

        lock (_pendingLock)
        {
            foreach (string directory in new string[] { _paths.ArtDirectory, GetDirectory() })
            {
                string? path = GetPath(directory, reference);

                if (path is not null && _streams.TryOpenExistingFileForRead(path, new string[] { directory },
                    _paths.RootDirectory, TrustedPathFailureMessage, out FileStream? stream, out trustedPath))
                {
                    return stream;
                }
            }
        }

        return null;
    }

    private void Retain(Guid runId, IEnumerable<GenerationReference> references)
    {
        lock (_pendingLock)
        {
            if (!_pendingHashes.TryGetValue(runId, out HashSet<string>? hashes))
            {
                hashes = new HashSet<string>(StringComparer.Ordinal);
                _pendingHashes.Add(runId, hashes);
            }

            hashes.UnionWith(references.Select(reference => reference.Hash));
        }
    }

    private string GetDirectory()
    {
        return GenerationReferenceFilePolicy.GetDirectory(_paths);
    }

    private IEnumerable<string> GetOwnedHashes()
    {
        string root = _paths.StateAttachmentsDirectory;

        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            TrustedPathGuard.EnsureTrustedWriteTarget(root, directory, TrustedPathFailureMessage);

            foreach (string path in Directory.EnumerateFiles(directory, $"*{GenerationReferenceFilePolicy.OwnerFileExtension}"))
            {
                if (!_streams.TryOpenExistingFileForRead(path, new string[] { root }, _paths.RootDirectory,
                    TrustedPathFailureMessage, out FileStream? stream, out _) || stream is null)
                {
                    continue;
                }

                using (stream)
                {
                    string hash = GenerationReferenceFilePolicy.ReadOwnerHash(stream)
                        ?? throw new IOException("A shared image owner record is invalid.");

                    yield return hash;
                }
            }
        }
    }

    private string? GetPath(string directory, GenerationReference reference)
    {
        return GenerationReferenceFilePolicy.IsHash(reference.Hash)
            && !string.IsNullOrWhiteSpace(reference.FileName)
            && SafeFileName.IsValid(reference.FileName)
            && !string.IsNullOrWhiteSpace(reference.ContentType)
            && _formats.TryGetByContentType(reference.ContentType, out IGenerationImageFormat? format)
            && format is not null
            ? Path.Combine(directory, string.Concat(reference.Hash, format.Extension))
            : null;
    }

    private async Task WriteAsync(string directory, string path, byte[] content, CancellationToken ct)
    {
        TrustedPathGuard.EnsureTrustedWriteTarget(directory, path, TrustedPathFailureMessage);

        if (File.Exists(path))
        {
            return;
        }

        string tempPath = AtomicFileWriteTempPath.CreateSibling(directory, Path.GetFileName(path));

        try
        {
            await using (FileStream stream = _streams.CreateNewFileForWrite(directory, tempPath, TrustedPathFailureMessage))
            {
                await stream.WriteAsync(content, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            TrustedPathGuard.ReplaceTrustedFile(directory, tempPath, path, TrustedPathFailureMessage);
        }
        finally
        {
            FileDeletion.DeleteIfExists(tempPath);
        }
    }
}
