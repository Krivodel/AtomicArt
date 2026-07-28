using Microsoft.Extensions.Options;

namespace AtomicArt.Desktop.Services.Gallery.State;

public sealed class GalleryOrderTimestampPolicy
{
    private readonly TimeSpan _timestampInterval;

    public GalleryOrderTimestampPolicy(IOptions<GalleryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _timestampInterval = TimeSpan.FromMilliseconds(
            options.Value.OrderTimestampIntervalMilliseconds);
    }

    public IReadOnlyList<DateTime> CreateForPrependedItems(
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

    public DateTime Normalize(DateTime timestamp)
    {
        DateTime utcTimestamp = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
        long normalizedTicks = utcTimestamp.Ticks
            - (utcTimestamp.Ticks % _timestampInterval.Ticks);

        return new DateTime(normalizedTicks, DateTimeKind.Utc);
    }

    public DateTime EnsureNewer(DateTime candidateUtc, DateTime olderUtc)
    {
        DateTime normalizedCandidateUtc = Normalize(candidateUtc);
        DateTime normalizedOlderUtc = Normalize(olderUtc);

        return normalizedCandidateUtc > normalizedOlderUtc
            ? normalizedCandidateUtc
            : AddInterval(normalizedOlderUtc);
    }

    private DateTime AddInterval(DateTime timestampUtc)
    {
        return AddIntervals(timestampUtc, 1);
    }

    private DateTime AddIntervals(DateTime timestampUtc, int intervalCount)
    {
        long ticksToAdd = checked(_timestampInterval.Ticks * intervalCount);

        return timestampUtc.AddTicks(ticksToAdd);
    }
}
