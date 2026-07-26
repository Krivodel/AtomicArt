namespace AtomicArt.Desktop.Services.Gallery.State;

internal static class GalleryOrderTimestampPolicy
{
    private static readonly TimeSpan TimestampInterval = TimeSpan.FromSeconds(2);

    public static IReadOnlyList<DateTime> CreateForPrependedItems(
        DateTime requestedAtUtc,
        DateTime? currentNewestTimestampUtc,
        int itemCount)
    {
        if (itemCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount));
        }

        DateTime oldestNewTimestampUtc = Normalize(requestedAtUtc);

        if (currentNewestTimestampUtc.HasValue)
        {
            DateTime normalizedCurrentNewestTimestampUtc =
                Normalize(currentNewestTimestampUtc.Value);

            if (oldestNewTimestampUtc <= normalizedCurrentNewestTimestampUtc)
            {
                oldestNewTimestampUtc = AddInterval(
                    normalizedCurrentNewestTimestampUtc);
            }
        }

        DateTime[] timestamps = new DateTime[itemCount];

        for (int index = 0; index < itemCount; index++)
        {
            int newerPositionCount = itemCount - index - 1;
            timestamps[index] = AddIntervals(
                oldestNewTimestampUtc,
                newerPositionCount);
        }

        return timestamps;
    }

    public static DateTime Normalize(DateTime timestamp)
    {
        DateTime utcTimestamp = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
        long normalizedTicks = utcTimestamp.Ticks
            - (utcTimestamp.Ticks % TimestampInterval.Ticks);

        return new DateTime(normalizedTicks, DateTimeKind.Utc);
    }

    public static DateTime EnsureNewer(DateTime candidateUtc, DateTime olderUtc)
    {
        DateTime normalizedCandidateUtc = Normalize(candidateUtc);
        DateTime normalizedOlderUtc = Normalize(olderUtc);

        return normalizedCandidateUtc > normalizedOlderUtc
            ? normalizedCandidateUtc
            : AddInterval(normalizedOlderUtc);
    }

    private static DateTime AddInterval(DateTime timestampUtc)
    {
        return AddIntervals(timestampUtc, 1);
    }

    private static DateTime AddIntervals(DateTime timestampUtc, int intervalCount)
    {
        long ticksToAdd = checked(TimestampInterval.Ticks * intervalCount);

        return timestampUtc.AddTicks(ticksToAdd);
    }
}
