using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed class GenerationReferenceStore : IGenerationReferenceStore
{
    private const string HistoryDirectoryName = "History";
    private const string TrustedPathFailureMessage = "The generation reference path is unsafe.";
    private const int HashLength = 64;

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
            string directory = GetDirectory();
            TrustedPathGuard.EnsureTrustedDirectoryExists(
                directory,
                path => Directory.CreateDirectory(path),
                TrustedPathFailureMessage);
            GenerationReference[] references = new GenerationReference[images.Count];

            for (int index = 0; index < images.Count; index++)
            {
                ct.ThrowIfCancellationRequested();
                AttachedImageDto image = images[index];
                string hash = Convert.ToHexStringLower(SHA256.HashData(image.Content));
                GenerationReference reference = new(hash, image.FileName, image.ContentType);
                string path = GetPath(directory, reference)
                    ?? throw new InvalidDataException("The generation reference format is unsupported.");
                await WriteAsync(directory, path, image.Content, ct).ConfigureAwait(false);
                references[index] = reference;
            }

            lock (_pendingLock)
            {
                _pendingHashes.Add(runId, references.Select(reference => reference.Hash)
                    .ToHashSet(StringComparer.Ordinal));
            }

            return references;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task<AttachedImageDto?> LoadAsync(GenerationReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return ReadAsync<AttachedImageDto>(
            reference,
            (stream, token) => ReadImageAsync(reference, stream, token),
            ct);
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
        using DataRootAccessLease lease = await _accessCoordinator.AcquireAccessAsync(ct)
            .ConfigureAwait(false);
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

    private FileStream? OpenTrustedFile(GenerationReference reference, out string? trustedPath)
    {
        ArgumentNullException.ThrowIfNull(reference);
        trustedPath = null;
        string directory = GetDirectory();
        string? path = GetPath(directory, reference);

        return path is not null && _streams.TryOpenExistingFileForRead(
                path,
                new string[] { directory },
                _paths.RootDirectory,
                TrustedPathFailureMessage,
                out FileStream? stream,
                out trustedPath)
            ? stream
            : null;
    }

    public void Release(Guid runId)
    {
        lock (_pendingLock)
        {
            _pendingHashes.Remove(runId);
        }
    }

    public async Task PruneAsync(IReadOnlySet<string> retainedHashes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retainedHashes);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            using DataRootAccessLease lease = await _accessCoordinator.AcquireAccessAsync(ct)
                .ConfigureAwait(false);
            string directory = GetDirectory();
            if (!Directory.Exists(directory))
            {
                return;
            }

            TrustedPathGuard.EnsureTrustedWriteTarget(_paths.StateAttachmentsDirectory, directory, TrustedPathFailureMessage);
            HashSet<string> pendingHashes;
            lock (_pendingLock)
            {
                pendingHashes = _pendingHashes.Values.SelectMany(hashes => hashes).ToHashSet(StringComparer.Ordinal);
            }

            foreach (string path in Directory.EnumerateFiles(directory))
            {
                ct.ThrowIfCancellationRequested();
                string hash = Path.GetFileNameWithoutExtension(path);
                if (!IsHash(hash) || !_formats.TryGetByFileName(path, out _)
                    || retainedHashes.Contains(hash) || pendingHashes.Contains(hash))
                {
                    continue;
                }

                TrustedPathGuard.EnsureTrustedWriteTarget(directory, path, TrustedPathFailureMessage);
                FileDeletion.DeleteIfExists(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not clean unused generation references.");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private string GetDirectory()
    {
        return Path.Combine(_paths.StateAttachmentsDirectory, HistoryDirectoryName);
    }

    private string? GetPath(string directory, GenerationReference reference)
    {
        return IsHash(reference.Hash)
            && !string.IsNullOrWhiteSpace(reference.FileName)
            && SafeFileName.IsValid(reference.FileName)
            && !string.IsNullOrWhiteSpace(reference.ContentType)
            && _formats.TryGetByContentType(reference.ContentType, out IGenerationImageFormat? format)
            && format is not null
            ? Path.Combine(directory, string.Concat(reference.Hash, format.Extension))
            : null;
    }

    private static bool IsHash(string? hash)
    {
        return hash is { Length: HashLength }
            && hash.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
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
