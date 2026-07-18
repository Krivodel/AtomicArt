using Xunit;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Concurrency;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class AttachedImagePreparationConcurrencyLimiterTests
{
    [Fact]
    public async Task WaitAsync_WhenLogicalProcessorLimitIsOccupied_WaitsForRelease()
    {
        AttachedImagePreparationConcurrencyLimiter limiter = new();

        await ConcurrencyLimiterAssertions.AssertBlocksNextWaitUntilReleaseAsync(
            limiter,
            AttachedImagePreparationConcurrencyLimiter.MaximumConcurrency);
    }
}
