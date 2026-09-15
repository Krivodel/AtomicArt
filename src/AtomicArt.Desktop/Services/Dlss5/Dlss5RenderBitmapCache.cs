using SkiaSharp;

namespace AtomicArt.Desktop.Services.Dlss5;

internal sealed class Dlss5RenderBitmapCache
{
    public const int DefaultMaxEntries = 6;
    public const long DefaultMaxBytes = 256L * 1024 * 1024;

    private const int MinimumEntryCount = 1;
    private const long MinimumByteCount = 1L;

    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private readonly object _sync = new();
    private readonly Dictionary<Dlss5RenderCacheKey, LinkedListNode<CacheEntry>> _entries = [];
    private readonly LinkedList<CacheEntry> _leastRecentlyUsed = [];
    private long _sizeBytes;

    internal Dlss5RenderBitmapCache()
        : this(DefaultMaxEntries, DefaultMaxBytes)
    {
    }

    internal Dlss5RenderBitmapCache(int maxEntries, long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, MinimumEntryCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, MinimumByteCount);

        _maxEntries = maxEntries;
        _maxBytes = maxBytes;
    }

    public bool TryGet(Dlss5RenderCacheKey key, out SKBitmap? bitmap)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
            {
                bitmap = null;
                return false;
            }

            _leastRecentlyUsed.Remove(node);
            _leastRecentlyUsed.AddFirst(node);
            bitmap = node.Value.Bitmap.Copy()
                ?? throw new InvalidDataException("The cached DLSS 5 result cannot be copied.");
            return true;
        }
    }

    public void Store(Dlss5RenderCacheKey key, SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        lock (_sync)
        {
            if (_entries.Remove(key, out LinkedListNode<CacheEntry>? previous))
            {
                _leastRecentlyUsed.Remove(previous);
                RemoveEntry(previous.Value);
            }

            long bitmapBytes = bitmap.ByteCount;
            LinkedListNode<CacheEntry> node = _leastRecentlyUsed.AddFirst(
                new CacheEntry(key, bitmap, bitmapBytes));
            _entries[key] = node;
            _sizeBytes += bitmapBytes;

            while ((_entries.Count > _maxEntries) || (_sizeBytes > _maxBytes))
            {
                LinkedListNode<CacheEntry> last = _leastRecentlyUsed.Last
                    ?? throw new InvalidOperationException("DLSS 5 render cache lost its last entry.");
                _leastRecentlyUsed.RemoveLast();
                _entries.Remove(last.Value.Key);
                RemoveEntry(last.Value);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            foreach (CacheEntry entry in _leastRecentlyUsed)
            {
                entry.Bitmap.Dispose();
            }

            _entries.Clear();
            _leastRecentlyUsed.Clear();
            _sizeBytes = 0;
        }
    }

    private void RemoveEntry(CacheEntry entry)
    {
        _sizeBytes -= entry.ByteCount;
        entry.Bitmap.Dispose();
    }

    private sealed record CacheEntry(Dlss5RenderCacheKey Key, SKBitmap Bitmap, long ByteCount);
}
