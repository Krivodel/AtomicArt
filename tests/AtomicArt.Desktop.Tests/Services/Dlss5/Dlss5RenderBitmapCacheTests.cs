using FluentAssertions;
using SkiaSharp;
using Xunit;

using AtomicArt.Desktop.Services.Dlss5;

namespace AtomicArt.Desktop.Tests.Services.Dlss5;

public sealed class Dlss5RenderBitmapCacheTests
{
    [Fact]
    public void Store_WhenByteBudgetIsExceeded_EvictsLeastRecentlyUsedBitmap()
    {
        using SKBitmap first = new(4, 4);
        using SKBitmap second = new(4, 4);
        long singleBitmapBytes = first.ByteCount;
        Dlss5RenderBitmapCache cache = new(maxEntries: 2, maxBytes: singleBitmapBytes);
        Dlss5RenderCacheKey firstKey = Dlss5RenderBitmapCacheTests.CreateKey(1);
        Dlss5RenderCacheKey secondKey = Dlss5RenderBitmapCacheTests.CreateKey(2);

        cache.Store(firstKey, first);
        cache.Store(secondKey, second);

        cache.TryGet(firstKey, out SKBitmap? evicted).Should().BeFalse();
        evicted.Should().BeNull();
        cache.TryGet(secondKey, out SKBitmap? retained).Should().BeTrue();
        retained.Should().NotBeNull();
        retained?.Dispose();
        cache.Clear();
    }

    [Fact]
    public void Store_WhenEntryExceedsByteBudget_DoesNotRetainBitmap()
    {
        using SKBitmap bitmap = new(4, 4);
        Dlss5RenderBitmapCache cache = new(maxEntries: 2, maxBytes: bitmap.ByteCount - 1);
        Dlss5RenderCacheKey key = Dlss5RenderBitmapCacheTests.CreateKey(1);

        cache.Store(key, bitmap);

        cache.TryGet(key, out SKBitmap? result).Should().BeFalse();
        result.Should().BeNull();
    }

    private static Dlss5RenderCacheKey CreateKey(long sourceGeneration)
    {
        return new Dlss5RenderCacheKey(
            sourceGeneration,
            4,
            4,
            Dlss5RenderSettings.Default);
    }
}
