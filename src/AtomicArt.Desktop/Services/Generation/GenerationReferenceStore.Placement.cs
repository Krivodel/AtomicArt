using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Generation;

internal sealed partial class GenerationReferenceStore
{
    private readonly Dictionary<Guid, HashSet<string>> _pendingResultHashes = [];
    private readonly Dictionary<Guid, HashSet<string>> _readPathHashes = [];
    private readonly HashSet<string> _galleryHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _retainedHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ownedHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deferredHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _releasedHashes = new(StringComparer.Ordinal);
    private TaskCompletionSource _readersChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _hasPlacementSnapshot;
    private bool _releaseWorkerRunning;

    public async Task RetainReadPathsAsync(Guid runId, IEnumerable<GenerationReference> references, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(references);
        GenerationReference[] retained = references.ToArray();
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            Retain(runId, retained);

            lock (_pendingLock)
            {
                if (!_readPathHashes.TryGetValue(runId, out HashSet<string>? hashes))
                {
                    hashes = new HashSet<string>(StringComparer.Ordinal);
                    _readPathHashes.Add(runId, hashes);
                }

                hashes.UnionWith(retained.Select(reference => reference.Hash));
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task PromoteAsync(Guid runId, IReadOnlyList<GenerationReference> references, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(references);

        while (true)
        {
            Task? readers = null;
            await _writeLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                using DataRootAccessLease access = await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);

                foreach (GenerationReference reference in references.DistinctBy(reference => reference.Hash))
                {
                    string? historyPath = GetPath(GetDirectory(), reference);

                    if (historyPath is null)
                    {
                        continue;
                    }

                    lock (_pendingLock)
                    {
                        bool pending = _pendingHashes.TryGetValue(runId, out HashSet<string>? retained)
                            && retained.Contains(reference.Hash);

                        if (!_galleryHashes.Contains(reference.Hash) || pending)
                        {
                            Retain(runId, new GenerationReference[] { reference });

                            if (!_pendingResultHashes.TryGetValue(runId, out HashSet<string>? hashes))
                            {
                                hashes = new HashSet<string>(StringComparer.Ordinal);
                                _pendingResultHashes.Add(runId, hashes);
                            }

                            hashes.Add(reference.Hash);
                        }
                    }

                    if (_hasPlacementSnapshot && _galleryHashes.Contains(reference.Hash)
                        && !_dirtyHashes.Contains(reference.Hash) && !_deferredHashes.Contains(reference.Hash))
                    {
                        continue;
                    }

                    string artPath = Path.Combine(_paths.ArtDirectory, Path.GetFileName(historyPath));

                    if (!File.Exists(historyPath))
                    {
                        continue;
                    }

                    lock (_pendingLock)
                    {
                        if (_readPathHashes.Values.Any(hashes => hashes.Contains(reference.Hash)))
                        {
                            readers = _readersChanged.Task;
                        }
                    }

                    if (readers is not null)
                    {
                        break;
                    }

                    await MoveImageAsync(reference.Hash, historyPath, artPath, ct).ConfigureAwait(false);
                    _dirtyHashes.Add(reference.Hash);

                    if (!_galleryHashes.Contains(reference.Hash)
                        && (_retainedHashes.Contains(reference.Hash) || _ownedHashes.Contains(reference.Hash)))
                    {
                        _deferredHashes.Add(reference.Hash);
                    }
                }
            }
            finally
            {
                _writeLock.Release();
            }

            if (readers is null)
            {
                return;
            }

            await readers.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    public Task SynchronizeAsync(IReadOnlySet<string> galleryHashes, IReadOnlySet<string> retainedHashes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(galleryHashes);
        ArgumentNullException.ThrowIfNull(retainedHashes);

        return SynchronizeCoreAsync(galleryHashes, retainedHashes, ct);
    }

    private GenerationReferenceUsage CapturePendingUsage()
    {
        lock (_pendingLock)
        {
            return new GenerationReferenceUsage(
                _pendingHashes.Values.SelectMany(hashes => hashes).ToHashSet(StringComparer.Ordinal),
                _pendingResultHashes.Values.SelectMany(hashes => hashes).ToHashSet(StringComparer.Ordinal),
                _readPathHashes.Values.SelectMany(hashes => hashes).ToHashSet(StringComparer.Ordinal));
        }
    }

    private IEnumerable<string> EnumerateStoredHashes()
    {
        foreach (string directory in new string[] { _paths.ArtDirectory, GetDirectory() })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            TrustedPathGuard.EnsureTrustedWriteTarget(_paths.RootDirectory, directory, TrustedPathFailureMessage);

            foreach (string path in Directory.EnumerateFiles(directory))
            {
                string hash = Path.GetFileNameWithoutExtension(path);

                if (GenerationReferenceFilePolicy.IsHash(hash) && _formats.TryGetByFileName(path, out _))
                {
                    yield return hash;
                }
            }
        }
    }

    private async Task<string?> HashFileAsync(string path, CancellationToken ct)
    {
        string directory = Path.GetDirectoryName(path) ?? throw new IOException(TrustedPathFailureMessage);

        if (!_streams.TryOpenExistingFileForRead(path, new string[] { directory }, _paths.RootDirectory,
            TrustedPathFailureMessage, out FileStream? stream, out _) || stream is null)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            return await GenerationReferenceFilePolicy.HashAsync(stream, ct).ConfigureAwait(false);
        }
    }

    private async Task MoveImageAsync(string hash, string sourcePath, string targetPath, CancellationToken ct)
    {
        string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? throw new IOException(TrustedPathFailureMessage);
        string targetDirectory = Path.GetDirectoryName(targetPath) ?? throw new IOException(TrustedPathFailureMessage);
        TrustedPathGuard.EnsureTrustedWriteTarget(sourceDirectory, sourcePath, TrustedPathFailureMessage);
        TrustedPathGuard.EnsureTrustedDirectoryExists(targetDirectory,
            path => Directory.CreateDirectory(path), TrustedPathFailureMessage);
        TrustedPathGuard.EnsureTrustedWriteTarget(targetDirectory, targetPath, TrustedPathFailureMessage);

        if (File.Exists(targetPath))
        {
            string? sourceHash = await HashFileAsync(sourcePath, ct).ConfigureAwait(false);
            string? targetHash = await HashFileAsync(targetPath, ct).ConfigureAwait(false);

            if (!string.Equals(sourceHash, hash, StringComparison.Ordinal)
                || !string.Equals(targetHash, hash, StringComparison.Ordinal))
            {
                throw new IOException("Shared image copies have inconsistent content.");
            }

            lock (_pendingLock)
            {
                TrustedPathGuard.DeleteTrustedFileIfExists(sourcePath, new string[] { sourceDirectory }, _paths.RootDirectory,
                    resolved => TrustedPathGuard.EnsureInsideDirectory(sourceDirectory, resolved, TrustedPathFailureMessage));
            }

            return;
        }

        lock (_pendingLock)
        {
            TrustedPathGuard.MoveTrustedFile(_paths.RootDirectory, sourcePath, targetPath, TrustedPathFailureMessage);
        }
    }

    private async Task ProcessReleasesAsync()
    {
        while (true)
        {
            string[] released;

            lock (_pendingLock)
            {
                if (_releasedHashes.Count == 0)
                {
                    _releaseWorkerRunning = false;

                    return;
                }

                released = _releasedHashes.ToArray();
                _releasedHashes.Clear();
            }

            await _writeLock.WaitAsync().ConfigureAwait(false);

            try
            {
                foreach (string hash in released.Where(hash => !_galleryHashes.Contains(hash) && !_retainedHashes.Contains(hash)))
                {
                    _dirtyHashes.Add(hash);
                }

                string[] deferred = released.Where(_deferredHashes.Contains).ToArray();

                if (!_hasPlacementSnapshot || deferred.Length == 0)
                {
                    continue;
                }

                using DataRootAccessLease access = await _accessCoordinator.AcquireAccessAsync(CancellationToken.None).ConfigureAwait(false);
                HashSet<string> owned = GetOwnedHashes().ToHashSet(StringComparer.Ordinal);
                GenerationReferenceUsage usage = CapturePendingUsage();

                foreach (string hash in deferred)
                {
                    await ReconcileHashAsync(hash, owned, usage, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not finish deferred image placement.");
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }

    private async Task ReconcileHashAsync(string hash, IReadOnlySet<string> ownedHashes,
        GenerationReferenceUsage usage, CancellationToken ct)
    {
        bool isResult = _galleryHashes.Contains(hash) || usage.ResultHashes.Contains(hash);
        bool isRetained = isResult || _retainedHashes.Contains(hash) || ownedHashes.Contains(hash);
        bool isNeeded = isRetained || usage.PendingHashes.Contains(hash);

        if (usage.ReadHashes.Contains(hash))
        {
            if (!isResult)
            {
                _deferredHashes.Add(hash);
            }

            return;
        }

        try
        {
            foreach (string extension in _formats.Formats.Select(format => format.Extension).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                string fileName = string.Concat(hash, extension);
                string artPath = Path.Combine(_paths.ArtDirectory, fileName);
                string historyPath = Path.Combine(GetDirectory(), fileName);

                if (!isNeeded)
                {
                    foreach (string path in new string[] { artPath, historyPath })
                    {
                        if (!File.Exists(path))
                        {
                            continue;
                        }

                        TrustedPathGuard.EnsureTrustedWriteTarget(_paths.RootDirectory, path, TrustedPathFailureMessage);

                        lock (_pendingLock)
                        {
                            string directory = Path.GetDirectoryName(path) ?? throw new IOException(TrustedPathFailureMessage);
                            TrustedPathGuard.DeleteTrustedFileIfExists(path, new string[] { directory }, _paths.RootDirectory,
                                resolved => TrustedPathGuard.EnsureInsideDirectory(directory, resolved, TrustedPathFailureMessage));
                        }
                    }
                }
                else
                {
                    string source = isResult ? historyPath : artPath;
                    string target = isResult ? artPath : historyPath;

                    if (File.Exists(source))
                    {
                        await MoveImageAsync(hash, source, target, ct).ConfigureAwait(false);
                    }
                }
            }

            _deferredHashes.Remove(hash);
            _dirtyHashes.Remove(hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _deferredHashes.Add(hash);
            _dirtyHashes.Add(hash);
            _logger.LogWarning(ex, "Could not place a shared generation image.");
        }
    }

    private void ReleaseCore(Guid runId)
    {
        bool startWorker = false;

        lock (_pendingLock)
        {
            if (_pendingHashes.Remove(runId, out HashSet<string>? pending))
            {
                _releasedHashes.UnionWith(pending);
            }

            if (_pendingResultHashes.Remove(runId, out HashSet<string>? results))
            {
                _releasedHashes.UnionWith(results);
            }

            if (_readPathHashes.Remove(runId, out HashSet<string>? readers))
            {
                _releasedHashes.UnionWith(readers);
                _readersChanged.TrySetResult();
                _readersChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            if (!_releaseWorkerRunning && _releasedHashes.Count > 0)
            {
                _releaseWorkerRunning = true;
                startWorker = true;
            }
        }

        if (startWorker)
        {
            _ = Task.Run(ProcessReleasesAsync);
        }
    }

    private async Task SynchronizeCoreAsync(IReadOnlySet<string>? galleryHashes,
        IReadOnlySet<string> retainedHashes, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            using DataRootAccessLease access = await _accessCoordinator.AcquireAccessAsync(ct).ConfigureAwait(false);
            HashSet<string> gallery = new(galleryHashes ?? _galleryHashes, StringComparer.Ordinal);
            HashSet<string> owned = GetOwnedHashes().ToHashSet(StringComparer.Ordinal);
            HashSet<string> candidates = new(_dirtyHashes, StringComparer.Ordinal);
            candidates.UnionWith(_deferredHashes);

            if (!_hasPlacementSnapshot)
            {
                candidates.UnionWith(EnumerateStoredHashes());
            }

            HashSet<string> changedResults = new(_galleryHashes, StringComparer.Ordinal);
            changedResults.SymmetricExceptWith(gallery);
            candidates.UnionWith(changedResults);
            candidates.UnionWith(_retainedHashes.Except(retainedHashes));
            candidates.UnionWith(_ownedHashes.Except(owned));
            _galleryHashes.Clear();
            _galleryHashes.UnionWith(gallery);
            _retainedHashes.Clear();
            _retainedHashes.UnionWith(retainedHashes);
            _ownedHashes.Clear();
            _ownedHashes.UnionWith(owned);
            _hasPlacementSnapshot = true;
            GenerationReferenceUsage usage = CapturePendingUsage();

            foreach (string hash in candidates.Where(GenerationReferenceFilePolicy.IsHash))
            {
                await ReconcileHashAsync(hash, owned, usage, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not synchronize shared generation images.");
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
