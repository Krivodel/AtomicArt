using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Contracts.Generation;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Notifications;

namespace AtomicArt.Desktop.Tests.Services.Notifications;

public sealed class GenerationNotificationServiceTests
{
    [Fact]
    public void Publish_WithParallelResultsAndRepeatedCompletion_NotifiesEachResultOnce()
    {
        TestGenerationLifecycleEventHub events = new();
        Mock<IGenerationNotificationPresenter> presenter = CreatePresenter();
        using GenerationNotificationService service = CreateService(events, new GenerationNotificationSettings(), presenter);
        Guid runId = Guid.NewGuid();
        GenerationItemDto first = CreateItem("First prompt");
        GenerationItemDto second = CreateItem("Second prompt");
        GenerationLifecycleEvent completed = new(runId, GenerationLifecycleStatus.Completed,
            null, new GenerationBatchDto(Guid.NewGuid(), new[] { first, second }), null);
        events.Publish(CreateStart(runId, "Original prompt"));

        events.Publish(completed);
        events.Publish(completed);

        presenter.Verify(target => target.ShowAsync(
            It.Is<GenerationNotification>(value => value.Prompt == first.Prompt && !value.IsError),
            It.IsAny<CancellationToken>()), Times.Once);
        presenter.Verify(target => target.ShowAsync(
            It.Is<GenerationNotification>(value => value.Prompt == second.Prompt && !value.IsError),
            It.IsAny<CancellationToken>()), Times.Once);
        presenter.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(GenerationLifecycleStatus.StartFailed)]
    [InlineData(GenerationLifecycleStatus.Failed)]
    public void Publish_WithFailure_UsesPromptFromTheMatchingRun(GenerationLifecycleStatus status)
    {
        TestGenerationLifecycleEventHub events = new();
        Mock<IGenerationNotificationPresenter> presenter = CreatePresenter();
        using GenerationNotificationService service = CreateService(events, new GenerationNotificationSettings(), presenter);
        Guid first = Guid.NewGuid();
        events.Publish(CreateStart(first, "Failed prompt"));
        events.Publish(CreateStart(Guid.NewGuid(), "Another prompt"));

        events.Publish(new GenerationLifecycleEvent(first, status, null, null, "error-code"));

        presenter.Verify(target => target.ShowAsync(
            It.Is<GenerationNotification>(value => value.Prompt == "Failed prompt"
                && value.IsError && value.FailureCode == "error-code"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Publish_WithCanceledRun_DoesNotNotify()
    {
        TestGenerationLifecycleEventHub events = new();
        Mock<IGenerationNotificationPresenter> presenter = CreatePresenter();
        using GenerationNotificationService service = CreateService(events, new GenerationNotificationSettings(), presenter);
        NanoBanana2GenerationLifecyclePublisher publisher = new(events);
        Guid runId = Guid.NewGuid();
        publisher.PublishStarted(runId, CreateSnapshot("Canceled"));
        GenerationRunState state = new();
        state.MarkStarted();

        publisher.PublishCanceledGeneration(runId, state);

        events.PublishedEvents.Last().IsCanceled.Should().BeTrue();
        events.PublishedEvents.Last().Status.Should().Be(GenerationLifecycleStatus.Failed);
        presenter.VerifyNoOtherCalls();
    }

    [Fact]
    public void SetEnabled_WhenDisabled_CancelsVisibleAndQueuedNotificationsAndSkipsFurtherResults()
    {
        TestGenerationLifecycleEventHub events = new();
        Mock<IGenerationNotificationPresenter> presenter = CreatePresenter();
        GenerationNotificationSettings settings = new();
        CancellationToken presentationToken = default;
        presenter.Setup(target => target.ShowAsync(It.IsAny<GenerationNotification>(), It.IsAny<CancellationToken>()))
            .Callback<GenerationNotification, CancellationToken>((_, token) => presentationToken = token)
            .Returns(Task.CompletedTask);
        using GenerationNotificationService service = CreateService(events, settings, presenter);
        Guid first = Guid.NewGuid();
        events.Publish(CreateStart(first, "First"));
        events.Publish(new GenerationLifecycleEvent(first, GenerationLifecycleStatus.Failed, null, null, null));
        Guid second = Guid.NewGuid();
        events.Publish(CreateStart(second, "Second"));

        settings.SetEnabled(false);
        events.Publish(new GenerationLifecycleEvent(second, GenerationLifecycleStatus.Failed, null, null, null));

        presentationToken.IsCancellationRequested.Should().BeTrue();
        presenter.Verify(target => target.ShowAsync(It.IsAny<GenerationNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Publish_WhenPresenterFails_DoesNotBreakOtherLifecycleSubscribers()
    {
        TestGenerationLifecycleEventHub events = new();
        Mock<IGenerationNotificationPresenter> presenter = CreatePresenter();
        presenter.Setup(target => target.ShowAsync(It.IsAny<GenerationNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Display unavailable"));
        using GenerationNotificationService service = CreateService(events, new GenerationNotificationSettings(), presenter);
        Guid runId = Guid.NewGuid();
        events.Publish(CreateStart(runId, "Prompt"));
        bool received = false;
        using IDisposable subscription = events.Subscribe(_ => received = true);

        events.Publish(new GenerationLifecycleEvent(runId, GenerationLifecycleStatus.Failed, null, null, null));

        received.Should().BeTrue();
    }

    private static Mock<IGenerationNotificationPresenter> CreatePresenter()
    {
        Mock<IGenerationNotificationPresenter> presenter = new();
        presenter.Setup(target => target.ShowAsync(It.IsAny<GenerationNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return presenter;
    }

    private static GenerationNotificationService CreateService(TestGenerationLifecycleEventHub events,
        GenerationNotificationSettings settings, Mock<IGenerationNotificationPresenter> presenter)
    {
        return new GenerationNotificationService(events, settings, presenter.Object,
            NullLogger<GenerationNotificationService>.Instance);
    }

    private static GenerationLifecycleEvent CreateStart(Guid runId, string prompt)
    {
        return new GenerationLifecycleEvent(runId, GenerationLifecycleStatus.StartRequested,
            CreateSnapshot(prompt), null, null);
    }

    private static GenerationStartSnapshot CreateSnapshot(string prompt)
    {
        return new GenerationStartSnapshot("codex", "Codex", prompt, "auto", "auto", 1, 0, DateTime.UnixEpoch);
    }

    private static GenerationItemDto CreateItem(string prompt)
    {
        return new GenerationItemDto(Guid.NewGuid(), "codex", "Codex", prompt, "auto", "auto",
            DateTime.UnixEpoch, GenerationItemStatus.Generated, null);
    }
}
