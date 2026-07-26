using Microsoft.Extensions.Logging;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Tests.Common;
using AtomicArt.Desktop.Tests.Services.Generation;

namespace AtomicArt.Desktop.Tests.Services.Gallery;

public sealed class GalleryLifecycleControllerTests
{
    private static readonly Guid CorrelationId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void StartRequested_DoesNotLogWarning()
    {
        VerifyWarningCountForStatus(GenerationLifecycleStatus.StartRequested, Times.Never());
    }

    [Fact]
    public void UnsupportedStatus_LogsWarning()
    {
        VerifyWarningCountForStatus((GenerationLifecycleStatus)int.MaxValue, Times.Once());
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

    [Fact]
    public async Task ElapsedRefresh_WhenWindowIsHidden_WaitsUntilWindowIsPresented()
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        TaskCompletionSource presentationSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource refreshSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bool isPresented = false;
        Mock<IWindowPresentationService> presentationServiceMock = new();
        presentationServiceMock
            .SetupGet(service => service.IsPresented)
            .Returns(() => isPresented);
        presentationServiceMock
            .Setup(service => service.WaitUntilPresentedAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => presentationSource.Task.WaitAsync(ct));
        Mock<IGalleryLifecycleViewState> viewStateMock = new();
        viewStateMock
            .Setup(viewState => viewState.RefreshElapsedTextAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => refreshSource.TrySetResult())
            .Returns(Task.CompletedTask);
        using GalleryLifecycleController controller = CreateController(
            lifecycleEventHub,
            Mock.Of<ILogger<GalleryLifecycleController>>(),
            viewState: viewStateMock.Object,
            windowPresentationService: presentationServiceMock.Object);

        viewStateMock.Verify(
            viewState => viewState.RefreshElapsedTextAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        isPresented = true;
        presentationSource.SetResult();
        await refreshSource.Task.WaitAsync(TimeSpan.FromSeconds(1));

        viewStateMock.Verify(
            viewState => viewState.RefreshElapsedTextAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void VerifyWarningCountForStatus(
        GenerationLifecycleStatus status,
        Times expectedCount)
    {
        TestGenerationLifecycleEventHub lifecycleEventHub = new();
        Mock<ILogger<GalleryLifecycleController>> loggerMock = new();
        using GalleryLifecycleController controller = CreateController(lifecycleEventHub, loggerMock.Object);

        lifecycleEventHub.Publish(CreateEvent(status));

        VerifyWarningCount(loggerMock, expectedCount);
    }

    private static GalleryLifecycleController CreateController(
        IGenerationLifecycleEventHub lifecycleEventHub,
        ILogger<GalleryLifecycleController> logger,
        IGenerationActivityTracker? activityTracker = null,
        IEnumerable<IGalleryLifecycleEventHandler>? lifecycleEventHandlers = null,
        IGalleryLifecycleViewState? viewState = null,
        IWindowPresentationService? windowPresentationService = null)
    {
        Mock<IGalleryLifecycleViewState> viewStateMock = new();
        viewStateMock
            .Setup(viewState => viewState.RefreshElapsedTextAsync(
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IWindowPresentationService> presentationServiceMock = new();
        presentationServiceMock
            .SetupGet(service => service.IsPresented)
            .Returns(true);
        presentationServiceMock
            .Setup(service => service.WaitUntilPresentedAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new GalleryLifecycleController(
            lifecycleEventHub,
            viewState ?? viewStateMock.Object,
            Mock.Of<IViewModelErrorHandler>(),
            activityTracker ?? TestGenerationActivityTrackerFactory.Create(),
            windowPresentationService ?? presentationServiceMock.Object,
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
        LoggerMockAssertions.VerifyLog(
            loggerMock,
            LogLevel.Warning,
            times);
    }
}
