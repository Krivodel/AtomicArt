using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class AttachedImagePreparationConcurrencyLimiterTests
{
    [Fact]
    public async Task WaitAsync_WhenLogicalProcessorLimitIsOccupied_WaitsForRelease()
    {
        AttachedImagePreparationConcurrencyLimiter limiter = new();

        for (int index = 0;
             index < AttachedImagePreparationConcurrencyLimiter.MaximumConcurrency;
             index++)
        {
            await limiter.WaitAsync(CancellationToken.None);
        }

        Task pendingWait = limiter.WaitAsync(CancellationToken.None);
        pendingWait.IsCompleted.Should().BeFalse();

        limiter.Release();
        await pendingWait.WaitAsync(TimeSpan.FromSeconds(1));

        for (int index = 0;
             index < AttachedImagePreparationConcurrencyLimiter.MaximumConcurrency;
             index++)
        {
            limiter.Release();
        }
    }
}
