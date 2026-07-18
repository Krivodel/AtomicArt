using Microsoft.Extensions.Logging;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;

namespace AtomicArt.Desktop.Tests.Services;

public sealed class GenerationLifecycleEventHubTests
{
    [Fact]
    public void Subscribe_WhenEventPublished_InvokesSubscriber()
    {
        Mock<ILogger<GenerationLifecycleEventHub>> loggerMock = new Mock<ILogger<GenerationLifecycleEventHub>>();
        GenerationLifecycleEventHub hub = new(loggerMock.Object);
        GenerationLifecycleEvent lifecycleEvent = CreateLifecycleEvent();
        GenerationLifecycleEvent? receivedEvent = null;
        hub.Subscribe(currentEvent =>
        {
            receivedEvent = currentEvent;
        });

        hub.Publish(lifecycleEvent);

        receivedEvent.Should().Be(lifecycleEvent);
    }

    [Fact]
    public void Subscribe_AfterDispose_DoesNotInvokeSubscriber()
    {
        Mock<ILogger<GenerationLifecycleEventHub>> loggerMock = new Mock<ILogger<GenerationLifecycleEventHub>>();
        GenerationLifecycleEventHub hub = new(loggerMock.Object);
        int invocationCount = 0;
        IDisposable subscription = hub.Subscribe(_ =>
        {
            invocationCount++;
        });
        subscription.Dispose();

        hub.Publish(CreateLifecycleEvent());

        invocationCount.Should().Be(0);
    }

    [Fact]
    public void Publish_WithMultipleSubscribers_NotifiesAll()
    {
        Mock<ILogger<GenerationLifecycleEventHub>> loggerMock = new Mock<ILogger<GenerationLifecycleEventHub>>();
        GenerationLifecycleEventHub hub = new(loggerMock.Object);
        int firstInvocationCount = 0;
        int secondInvocationCount = 0;
        hub.Subscribe(_ =>
        {
            firstInvocationCount++;
        });
        hub.Subscribe(_ =>
        {
            secondInvocationCount++;
        });

        hub.Publish(CreateLifecycleEvent());

        firstInvocationCount.Should().Be(1);
        secondInvocationCount.Should().Be(1);
    }

    [Fact]
    public void Publish_WhenSubscriberThrows_LogsErrorAndContinuesSubscribers()
    {
        Mock<ILogger<GenerationLifecycleEventHub>> loggerMock = new Mock<ILogger<GenerationLifecycleEventHub>>();
        GenerationLifecycleEventHub hub = new(loggerMock.Object);
        InvalidOperationException exception = new("Subscriber failed.");
        int successfulInvocationCount = 0;
        hub.Subscribe(_ => throw exception);
        hub.Subscribe(_ =>
        {
            successfulInvocationCount++;
        });

        hub.Publish(CreateLifecycleEvent());

        successfulInvocationCount.Should().Be(1);
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    (state.ToString() ?? string.Empty).Contains(
                        "Generation lifecycle subscriber failed",
                        StringComparison.Ordinal)),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static GenerationLifecycleEvent CreateLifecycleEvent()
    {
        return new GenerationLifecycleEvent(
            Guid.NewGuid(),
            GenerationLifecycleStatus.StartRequested,
            null,
            null,
            null);
    }
}
