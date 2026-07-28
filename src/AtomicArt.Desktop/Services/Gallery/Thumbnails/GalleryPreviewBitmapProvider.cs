using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Avalonia.Media.Imaging;

namespace AtomicArt.Desktop.Services.Gallery.Thumbnails;

internal sealed class GalleryPreviewBitmapProvider :
    IGalleryPreviewBitmapProvider,
    IDisposable
{
    private const int EstimatedBytesPerPixel = 4;

    private readonly object _sync = new();
    private readonly IGalleryPreviewBitmapLoader _loader;
    private readonly ILogger<GalleryPreviewBitmapProvider> _logger;
    private readonly Dictionary<string, CacheEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly long _maximumCacheSizeBytes;

    private long _accessSequence;
    private long _cachedSizeBytes;
    private int _activeLoadCount;
    private bool _lifetimeCancellationDisposed;
    private bool _disposed;

    public GalleryPreviewBitmapProvider(
        IGalleryPreviewBitmapLoader loader,
        ILogger<GalleryPreviewBitmapProvider> logger,
        IOptions<GalleryOptions> options)
        : this(loader, logger, GetMaximumCacheSizeBytes(options))
    {
    }

    internal GalleryPreviewBitmapProvider(
        IGalleryPreviewBitmapLoader loader,
        ILogger<GalleryPreviewBitmapProvider> logger,
        long maximumCacheSizeBytes)
    {
        if (maximumCacheSizeBytes <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCacheSizeBytes),
                maximumCacheSizeBytes,
                "Gallery preview bitmap cache size must be positive.");
        }

        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maximumCacheSizeBytes = maximumCacheSizeBytes;
    }

    public async Task<GalleryPreviewBitmapLease?> AcquireAsync(
        string imagePath,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        CacheEntry entry;
        bool startLoad = false;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_entries.TryGetValue(imagePath, out CacheEntry? existingEntry))
            {
                entry = new CacheEntry(imagePath, _lifetimeCancellation.Token);
                _entries.Add(imagePath, entry);
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
                    || !_entries.TryGetValue(imagePath, out CacheEntry? activeEntry)
                    || !ReferenceEquals(activeEntry, entry)
                    || !ReferenceEquals(entry.Bitmap, bitmap))
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
        }
    }

    private static long CalculateSizeBytes(Bitmap bitmap)
    {
        return checked(
            (long)bitmap.PixelSize.Width
            * bitmap.PixelSize.Height
            * EstimatedBytesPerPixel);
    }

    private static long GetMaximumCacheSizeBytes(
        IOptions<GalleryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Value.MaximumPreviewCacheSizeBytes;
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
                "Gallery preview bitmap cache update failed unexpectedly.");
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

        try
        {
            bitmap = await _loader
                .LoadAsync(entry.ImagePath, entry.LoadCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (entry.LoadCancellation.IsCancellationRequested)
        {
            CompleteFailedEntry(entry);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gallery preview bitmap loading failed unexpectedly.");
            CompleteFailedEntry(entry);
            return;
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
                || !_entries.TryGetValue(entry.ImagePath, out CacheEntry? activeEntry)
                || !ReferenceEquals(activeEntry, entry))
            {
                disposeBitmap = true;
            }
            else
            {
                entry.Bitmap = bitmap;
                entry.SizeBytes = CalculateSizeBytes(bitmap);
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
        }
    }

    private void CompleteFailedEntry(CacheEntry entry)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(entry.ImagePath, out CacheEntry? activeEntry)
                && ReferenceEquals(activeEntry, entry))
            {
                _entries.Remove(entry.ImagePath);
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
                && _entries.TryGetValue(entry.ImagePath, out CacheEntry? activeEntry)
                && ReferenceEquals(activeEntry, entry))
            {
                _entries.Remove(entry.ImagePath);
                loadCancellation = entry.LoadCancellation;
            }

            TouchEntry(entry);
            EvictEntries();
        }

        loadCancellation?.Cancel();
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

            _entries.Remove(candidate.ImagePath);
            _cachedSizeBytes -= candidate.SizeBytes;
            candidate.Bitmap = null;
            bitmap.Dispose();
        }
    }

    private sealed class CacheEntry
    {
        public string ImagePath { get; }
        public TaskCompletionSource<Bitmap?> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenSource LoadCancellation { get; }
        public Bitmap? Bitmap { get; set; }
        public int ActiveLeaseCount { get; set; }
        public int PendingLeaseCount { get; set; }
        public long LastAccessSequence { get; set; }
        public long SizeBytes { get; set; }

        public CacheEntry(string imagePath, CancellationToken lifetimeToken)
        {
            ImagePath = imagePath;
            LoadCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        }
    }
}
