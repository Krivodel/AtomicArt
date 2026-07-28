using Xunit;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Concurrency;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class AttachedImagePreparationConcurrencyLimiterTests
{
    [Fact]
    public async Task WaitAsync_WhenConfiguredLimitIsOccupied_WaitsForRelease()
    {
        const int maximumConcurrency = 2;
        AttachedImagePreparationConcurrencyLimiter limiter = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper(
                attachedImagePreparationConcurrency: maximumConcurrency));

        await ConcurrencyLimiterAssertions.AssertBlocksNextWaitUntilReleaseAsync(
            limiter,
            maximumConcurrency);
    }
}
