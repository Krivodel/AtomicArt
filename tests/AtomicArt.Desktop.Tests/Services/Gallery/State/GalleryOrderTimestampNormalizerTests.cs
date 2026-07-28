using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Gallery.State;
using AtomicArt.Desktop.Tests.TestDoubles;

namespace AtomicArt.Desktop.Tests.Services.Gallery.State;

public sealed class GalleryOrderTimestampNormalizerTests
{
    private static readonly DateTime BaseTimestampUtc = new(
        2026,
        7,
        26,
        12,
        0,
        0,
        DateTimeKind.Utc);

    private readonly GalleryOrderTimestampNormalizer _normalizer =
        TestApiConfiguration.CreateGalleryOrderTimestampNormalizer();

    [Fact]
    public void Normalize_WithLegacyItems_AssignsDescendingTimestampsInGalleryOrder()
    {
        GalleryItemState topItem = GalleryItemStateTestFactory.CreateGenerated(
            createdAtUtc: BaseTimestampUtc);
        GalleryItemState middleItem = GalleryItemStateTestFactory.CreateGenerated(
            createdAtUtc: BaseTimestampUtc.AddSeconds(4));
        GalleryItemState bottomItem = GalleryItemStateTestFactory.CreateGenerated(
            createdAtUtc: BaseTimestampUtc.AddSeconds(2));

        IReadOnlyList<GalleryItemState> normalizedItems =
            _normalizer.Normalize(
                [topItem, middleItem, bottomItem]);

        normalizedItems.Select(item => item.GalleryOrderTimestampUtc).Should()
            .Equal(
                BaseTimestampUtc.AddSeconds(6),
                BaseTimestampUtc.AddSeconds(4),
                BaseTimestampUtc.AddSeconds(2));
    }

    [Fact]
    public void Normalize_WithValidTimestamps_PreservesValues()
    {
        GalleryItemState topItem = GalleryItemStateTestFactory.CreateGenerated(
            galleryOrderTimestampUtc: BaseTimestampUtc.AddSeconds(2));
        GalleryItemState bottomItem = GalleryItemStateTestFactory.CreateGenerated(
            galleryOrderTimestampUtc: BaseTimestampUtc);

        IReadOnlyList<GalleryItemState> normalizedItems =
            _normalizer.Normalize([topItem, bottomItem]);

        normalizedItems.Select(item => item.GalleryOrderTimestampUtc).Should()
            .Equal(
                BaseTimestampUtc.AddSeconds(2),
                BaseTimestampUtc);
    }
}
