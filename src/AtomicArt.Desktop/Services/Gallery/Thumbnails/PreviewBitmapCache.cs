using Microsoft.Extensions.Logging;

using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class PreviewBitmapCache<TKey, TCategory> : IDisposable where TKey : notnull
{
    private const int EstimatedBytesPerPixel = 4;

    private readonly object _sync = new();
    private readonly Func<TKey, CancellationToken, Task<Bitmap?>> _load;
    private readonly ILogger<TCategory> _logger;
    private readonly Dictionary<TKey, CacheEntry> _entries;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _loadSlots;
    private readonly long _maximumCacheSizeBytes;
    private long _accessSequence;
    private long _cachedSizeBytes;
    private int _activeLoadCount;
    private bool _lifetimeCancellationDisposed;
    private bool _disposed;

    public PreviewBitmapCache(
        Func<TKey, CancellationToken, Task<Bitmap?>> load,
        ILogger<TCategory> logger,
        long maximumCacheSizeBytes,
        IEqualityComparer<TKey>? comparer = null,
        int maximumConcurrentLoads = int.MaxValue)
    {
        if (maximumCacheSizeBytes <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCacheSizeBytes),
                maximumCacheSizeBytes,
                "Preview bitmap cache size must be positive.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrentLoads);

        _load = load ?? throw new ArgumentNullException(nameof(load));
        _entries = new Dictionary<TKey, CacheEntry>(comparer);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maximumCacheSizeBytes = maximumCacheSizeBytes;
        _loadSlots = new SemaphoreSlim(maximumConcurrentLoads, maximumConcurrentLoads);
    }

    public async Task<GalleryPreviewBitmapLease?> AcquireAsync(
        TKey key,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);
        CacheEntry entry;
        bool startLoad = false;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_entries.TryGetValue(key, out CacheEntry? existingEntry))
            {
                entry = new CacheEntry(key, _lifetimeCancellation.Token);
                _entries.Add(key, entry);
                _activeLoadCount++;
                startLoad = true;
            }
            else
            {
                entry = existingEntry;
            }

            entry.PendingLeaseCount++;
            TouchEntry(entry);
        }

        if (startLoad)
        {
            _ = LoadEntryAsync(entry);
        }

        bool pendingLease = true;

        try
        {
            Bitmap? bitmap = await entry.Completion.Task
                .WaitAsync(ct)
                .ConfigureAwait(false);

            if (bitmap is null)
            {
                return null;
            }

            lock (_sync)
            {
                if (_disposed
                    || !_entries.TryGetValue(key, out CacheEntry? activeEntry)
                    || !object.ReferenceEquals(activeEntry, entry)
                    || !object.ReferenceEquals(entry.Bitmap, bitmap))
                {
                    return null;
                }

                entry.PendingLeaseCount--;
                pendingLease = false;
                entry.ActiveLeaseCount++;
                TouchEntry(entry);

                return new GalleryPreviewBitmapLease(
                    bitmap,
                    () => Release(entry));
            }
        }
        finally
        {
            if (pendingLease)
            {
                ReleasePendingLease(entry);
            }
        }
    }

    public void Dispose()
    {
        List<Bitmap> bitmaps = [];
        List<TaskCompletionSource<Bitmap?>> completions = [];
        bool disposeLifetimeCancellation = false;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (CacheEntry entry in _entries.Values)
            {
                if (entry.Bitmap is not null)
                {
                    bitmaps.Add(entry.Bitmap);
                }

                completions.Add(entry.Completion);
            }

            _entries.Clear();
            _cachedSizeBytes = 0L;

            if (_activeLoadCount == 0)
            {
                _lifetimeCancellationDisposed = true;
                disposeLifetimeCancellation = true;
            }
        }

        _lifetimeCancellation.Cancel();

        foreach (TaskCompletionSource<Bitmap?> completion in completions)
        {
            completion.TrySetResult(null);
        }

        foreach (Bitmap bitmap in bitmaps)
        {
            bitmap.Dispose();
        }

        if (disposeLifetimeCancellation)
        {
            _lifetimeCancellation.Dispose();
            _loadSlots.Dispose();
        }
    }

    private static long CalculateSizeBytes(Bitmap bitmap)
    {
        return checked(
            (long)bitmap.PixelSize.Width
            * bitmap.PixelSize.Height
            * EstimatedBytesPerPixel);
    }

    private async Task LoadEntryAsync(CacheEntry entry)
    {
        try
        {
            await LoadEntryCoreAsync(entry).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Preview bitmap cache update failed unexpectedly.");
            CompleteFailedEntry(entry);
        }
        finally
        {
            entry.LoadCancellation.Dispose();
            CompleteActiveLoad();
        }
    }

    private async Task LoadEntryCoreAsync(CacheEntry entry)
    {
        Bitmap? bitmap;
        bool slotAcquired = false;

        try
        {
            await _loadSlots.WaitAsync(entry.LoadCancellation.Token).ConfigureAwait(false);
            slotAcquired = true;
            bitmap = await _load(entry.Key, entry.LoadCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (entry.LoadCancellation.IsCancellationRequested)
        {
            CompleteFailedEntry(entry);

            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preview bitmap loading failed unexpectedly.");
            CompleteFailedEntry(entry);

            return;
        }
        finally
        {
            if (slotAcquired)
            {
                _loadSlots.Release();
            }
        }

        if (bitmap is null)
        {
            CompleteFailedEntry(entry);

            return;
        }

        bool disposeBitmap = false;

        lock (_sync)
        {
            if (_disposed
                || !_entries.TryGetValue(entry.Key, out CacheEntry? activeEntry)
                || !object.ReferenceEquals(activeEntry, entry))
            {
                disposeBitmap = true;
            }
            else
            {
                entry.Bitmap = bitmap;
                entry.SizeBytes = PreviewBitmapCache<TKey, TCategory>.CalculateSizeBytes(bitmap);
                _cachedSizeBytes += entry.SizeBytes;
                TouchEntry(entry);
                EvictEntries();
            }
        }

        if (disposeBitmap)
        {
            bitmap.Dispose();
            entry.Completion.TrySetResult(null);

            return;
        }

        entry.Completion.TrySetResult(bitmap);
    }

    private void CompleteActiveLoad()
    {
        bool disposeLifetimeCancellation = false;

        lock (_sync)
        {
            _activeLoadCount--;

            if (_disposed
                && (_activeLoadCount == 0)
                && !_lifetimeCancellationDisposed)
            {
                _lifetimeCancellationDisposed = true;
                disposeLifetimeCancellation = true;
            }
        }

        if (disposeLifetimeCancellation)
        {
            _lifetimeCancellation.Dispose();
            _loadSlots.Dispose();
        }
    }

    private void CompleteFailedEntry(CacheEntry entry)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(entry.Key, out CacheEntry? activeEntry)
                && object.ReferenceEquals(activeEntry, entry))
            {
                _entries.Remove(entry.Key);
            }
        }

        entry.Completion.TrySetResult(null);
    }

    private void Release(CacheEntry entry)
    {
        lock (_sync)
        {
            if (entry.ActiveLeaseCount > 0)
            {
                entry.ActiveLeaseCount--;
            }

            TouchEntry(entry);
            EvictEntries();
        }
    }

    private void ReleasePendingLease(CacheEntry entry)
    {
        CancellationTokenSource? loadCancellation = null;

        lock (_sync)
        {
            if (entry.PendingLeaseCount > 0)
            {
                entry.PendingLeaseCount--;
            }

            if ((entry.PendingLeaseCount == 0)
                && (entry.ActiveLeaseCount == 0)
                && (entry.Bitmap is null)
                && !entry.Completion.Task.IsCompleted
                && _entries.TryGetValue(entry.Key, out CacheEntry? activeEntry)
                && object.ReferenceEquals(activeEntry, entry))
            {
                _entries.Remove(entry.Key);
                loadCancellation = entry.LoadCancellation;
            }

            TouchEntry(entry);
            EvictEntries();
        }

        if (loadCancellation is not null)
        {
            try
            {
                loadCancellation.Cancel();
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogDebug(ex, "Preview loading completed before its cancellation request");
            }
        }
    }

    private void TouchEntry(CacheEntry entry)
    {
        entry.LastAccessSequence = ++_accessSequence;
    }

    private void EvictEntries()
    {
        while (_cachedSizeBytes > _maximumCacheSizeBytes)
        {
            CacheEntry? candidate = _entries.Values
                .Where(entry => entry.Bitmap is not null
                    && entry.ActiveLeaseCount == 0
                    && entry.PendingLeaseCount == 0)
                .OrderBy(entry => entry.LastAccessSequence)
                .FirstOrDefault();

            if (candidate?.Bitmap is not Bitmap bitmap)
            {
                return;
            }

            _entries.Remove(candidate.Key);
            _cachedSizeBytes -= candidate.SizeBytes;
            candidate.Bitmap = null;
            bitmap.Dispose();
        }
    }

    private sealed class CacheEntry
    {
        public TKey Key { get; }
        public TaskCompletionSource<Bitmap?> Completion { get; } =
            new TaskCompletionSource<Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenSource LoadCancellation { get; }
        public Bitmap? Bitmap { get; set; }
        public int ActiveLeaseCount { get; set; }
        public int PendingLeaseCount { get; set; }
        public long LastAccessSequence { get; set; }
        public long SizeBytes { get; set; }

        public CacheEntry(TKey key, CancellationToken lifetimeToken)
        {
            Key = key;
            LoadCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        }
    }
}
