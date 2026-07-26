namespace AtomicArt.Desktop.Services.Gallery.State;

internal static class GalleryOrderTimestampNormalizer
{
    public static IReadOnlyList<GalleryItemState> Normalize(
        IReadOnlyList<GalleryItemState> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        GalleryItemState[] normalizedItems = items.ToArray();
        DateTime? olderTimestampUtc = null;

        for (int index = normalizedItems.Length - 1; index >= 0; index--)
        {
            GalleryItemState item = normalizedItems[index];
            DateTime candidateTimestampUtc = item.GalleryOrderTimestampUtc
                ?? item.CreatedAtUtc;
            DateTime normalizedTimestampUtc = olderTimestampUtc.HasValue
                ? GalleryOrderTimestampPolicy.EnsureNewer(
                    candidateTimestampUtc,
                    olderTimestampUtc.Value)
                : GalleryOrderTimestampPolicy.Normalize(candidateTimestampUtc);

            if (item.GalleryOrderTimestampUtc != normalizedTimestampUtc)
            {
                normalizedItems[index] =
                    GalleryItemStateMapper.WithGalleryOrderTimestamp(
                        item,
                        normalizedTimestampUtc);
            }

            olderTimestampUtc = normalizedTimestampUtc;
        }

        return normalizedItems;
    }
}
