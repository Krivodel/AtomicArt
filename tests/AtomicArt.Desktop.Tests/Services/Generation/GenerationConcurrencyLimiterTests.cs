using Xunit;

using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Concurrency;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationConcurrencyLimiterTests
{
    [Fact]
    public async Task WaitAsync_WhenLimitReached_BlocksNextWaitUntilRelease()
    {
        const int maximumConcurrency = 2;
        GenerationConcurrencyLimiter limiter = new(
            TestApiConfiguration.CreateGenerationOptionsWrapper(
                maxConcurrentGenerations: maximumConcurrency));

        await ConcurrencyLimiterAssertions.AssertBlocksNextWaitUntilReleaseAsync(
            limiter,
            maximumConcurrency);
    }
}
