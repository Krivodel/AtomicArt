using Microsoft.Extensions.Options;

using FluentAssertions;
using Xunit;

using AtomicArt.Api.Generation;

namespace AtomicArt.Api.Tests.Generation;

public sealed class GenerationRequestConcurrencyLimiterTests
{
    [Fact]
    public void TryAcquire_AfterLeaseReleased_AllowsNextAttempt()
    {
        GenerationRequestConcurrencyLimiter limiter = new(
            Options.Create(new GenerationServerOptions
            {
                MaxConcurrentGenerations = 1
            }));
        IDisposable? firstLease = limiter.TryAcquire();

        IDisposable? rejectedLease = limiter.TryAcquire();
        firstLease?.Dispose();
        IDisposable? nextLease = limiter.TryAcquire();

        firstLease.Should().NotBeNull();
        rejectedLease.Should().BeNull();
        nextLease.Should().NotBeNull();
        nextLease?.Dispose();
    }
}
