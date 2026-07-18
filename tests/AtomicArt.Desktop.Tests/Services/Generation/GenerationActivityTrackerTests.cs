using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Generation;

public sealed class GenerationActivityTrackerTests
{
    private static readonly Guid CorrelationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task WaitUntilIdleAsync_WithRequestAndPersistence_WaitsForBothPhases()
    {
        IGenerationActivityTracker tracker = TestGenerationActivityTrackerFactory.Create();
        tracker.Start(CorrelationId, GenerationActivityPhase.GenerationRequest);
        tracker.Start(CorrelationId, GenerationActivityPhase.ResultPersistence);
        Task idleTask = tracker.WaitUntilIdleAsync(CancellationToken.None);

        tracker.Complete(CorrelationId, GenerationActivityPhase.GenerationRequest);

        tracker.IsActive.Should().BeTrue();
        idleTask.IsCompleted.Should().BeFalse();

        tracker.Complete(CorrelationId, GenerationActivityPhase.ResultPersistence);
        await idleTask.WaitAsync(TimeSpan.FromSeconds(1));

        tracker.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Start_WithFirstActivity_RaisesActivityChanged()
    {
        IGenerationActivityTracker tracker = TestGenerationActivityTrackerFactory.Create();
        int eventCount = 0;
        tracker.ActivityChanged += (_, _) => eventCount++;

        tracker.Start(CorrelationId, GenerationActivityPhase.GenerationRequest);

        eventCount.Should().Be(1);
        tracker.IsActive.Should().BeTrue();
    }
}
