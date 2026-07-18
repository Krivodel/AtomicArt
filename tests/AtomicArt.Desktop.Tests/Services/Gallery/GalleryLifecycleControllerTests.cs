using Microsoft.Extensions.Logging;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryLifecycleControllerTests
{
    private static readonly Guid CorrelationId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void StartRequested_DoesNotLogWarning()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        Mock<ILogger<GalleryLifecycleController>> loggerMock = new();
        using GalleryLifecycleController controller = CreateController(lifecycleEventHub, loggerMock.Object);

        lifecycleEventHub.Publish(CreateEvent(GenerationLifecycleStatus.StartRequested));

        VerifyWarningCount(loggerMock, Times.Never());
    }

    [Fact]
    public void UnsupportedStatus_LogsWarning()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        Mock<ILogger<GalleryLifecycleController>> loggerMock = new();
        using GalleryLifecycleController controller = CreateController(lifecycleEventHub, loggerMock.Object);

        lifecycleEventHub.Publish(CreateEvent((GenerationLifecycleStatus)int.MaxValue));

        VerifyWarningCount(loggerMock, Times.Once());
    }

    [Fact]
    public async Task Completed_WithPendingPersistence_KeepsGenerationActiveUntilHandlerCompletes()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        IGenerationActivityTracker activityTracker = TestGenerationActivityTrackerFactory.Create();
        TaskCompletionSource handlerCompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IGalleryLifecycleEventHandler> handlerMock = new();
        handlerMock
            .SetupGet(handler => handler.Status)
            .Returns(GenerationLifecycleStatus.Completed);
        handlerMock
            .Setup(handler => handler.HandleAsync(
                It.IsAny<GenerationLifecycleEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(handlerCompletionSource.Task);
        using GalleryLifecycleController controller = CreateController(
            lifecycleEventHub,
            Mock.Of<ILogger<GalleryLifecycleController>>(),
            activityTracker,
            [handlerMock.Object]);

        lifecycleEventHub.Publish(CreateEvent(GenerationLifecycleStatus.Completed));

        activityTracker.IsActive.Should().BeTrue();

        handlerCompletionSource.SetResult();
        await activityTracker
            .WaitUntilIdleAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(1));

        activityTracker.IsActive.Should().BeFalse();
    }

    private static GalleryLifecycleController CreateController(
        IGenerationLifecycleEventHub lifecycleEventHub,
        ILogger<GalleryLifecycleController> logger,
        IGenerationActivityTracker? activityTracker = null,
        IEnumerable<IGalleryLifecycleEventHandler>? lifecycleEventHandlers = null)
    {
        Mock<IGalleryLifecycleViewState> viewStateMock = new();
        viewStateMock
            .Setup(viewState => viewState.RefreshElapsedTextAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new GalleryLifecycleController(
            lifecycleEventHub,
            viewStateMock.Object,
            Mock.Of<IViewModelErrorHandler>(),
            activityTracker ?? TestGenerationActivityTrackerFactory.Create(),
            lifecycleEventHandlers ?? [],
            logger);
    }

    private static GenerationLifecycleEvent CreateEvent(GenerationLifecycleStatus status)
    {
        GenerationBatchDto? batch = status == GenerationLifecycleStatus.Completed
            ? new GenerationBatchDto(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                [])
            : null;

        return new GenerationLifecycleEvent(
            CorrelationId,
            status,
            null,
            batch,
            null);
    }

    private static void VerifyWarningCount(
        Mock<ILogger<GalleryLifecycleController>> loggerMock,
        Times times)
    {
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
