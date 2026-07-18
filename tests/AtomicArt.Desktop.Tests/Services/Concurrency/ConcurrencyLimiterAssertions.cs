using FluentAssertions;

using AtomicArt.Desktop.Services.Concurrency;

namespace AtomicArt.Desktop.Tests.Services.Concurrency;

internal static class ConcurrencyLimiterAssertions
{
    public static async Task AssertBlocksNextWaitUntilReleaseAsync(
        SemaphoreConcurrencyLimiter limiter,
        int maximumConcurrency)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrency);

        for (int index = 0; index < maximumConcurrency; index++)
        {
            await limiter.WaitAsync(CancellationToken.None);
        }

        Task pendingWait = limiter.WaitAsync(CancellationToken.None);

        pendingWait.IsCompleted.Should().BeFalse();

        limiter.Release();
        await pendingWait.WaitAsync(TimeSpan.FromSeconds(1));

        for (int index = 0; index < maximumConcurrency; index++)
        {
            limiter.Release();
        }
    }
}
