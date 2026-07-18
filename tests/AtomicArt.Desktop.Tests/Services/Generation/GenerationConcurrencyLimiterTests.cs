using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationConcurrencyLimiterTests
{
    [Fact]
    public async Task WaitAsync_WhenLimitReached_BlocksNextWaitUntilRelease()
    {
        GenerationConcurrencyLimiter limiter = new();

        for (int i = 0; i < GenerationConcurrencyLimiter.MaxConcurrentGenerations; i++)
        {
            await limiter.WaitAsync(CancellationToken.None);
        }

        Task pendingWait = limiter.WaitAsync(CancellationToken.None);

        pendingWait.IsCompleted.Should().BeFalse();

        limiter.Release();
        await pendingWait.WaitAsync(TimeSpan.FromSeconds(1));

        for (int i = 0; i < GenerationConcurrencyLimiter.MaxConcurrentGenerations; i++)
        {
            limiter.Release();
        }
    }
}
