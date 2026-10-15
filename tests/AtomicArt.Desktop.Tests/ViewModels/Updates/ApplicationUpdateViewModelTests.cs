using FluentAssertions;
using Moq;
using Xunit;

using CommunityToolkit.Mvvm.Messaging;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Localization;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.Tests.Services;
using AtomicArt.Desktop.Tests.Services.Generation;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Desktop.Tests.ViewModels.Gallery;
using AtomicArt.Desktop.ViewModels.Updates;

namespace AtomicArt.Desktop.Tests.ViewModels.Updates;

public sealed class ApplicationUpdateViewModelTests
{
    private const string UpdateVersion = "1.2.3";
    private static readonly Guid CorrelationId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task StartMonitoringCommand_WithUpdateDuringGeneration_OffersWaitAndUpdate()
    {
        using ApplicationUpdateTestContext context = new();
        context.StartGeneration();

        await StartMonitoringAsync(context.ViewModel);

        context.ViewModel.IsNotificationOpen.Should().BeTrue();
        context.ViewModel.State.Should().Be(ApplicationUpdateState.Available);
        context.ViewModel.UpdateActionText.Should().Be(TestLocalizationTextProvider.Default.Get(UpdateLocalizationKeys.Actions.WaitAndInstall));
        context.ViewModel.Message.Should().Contain(UpdateVersion);
    }

    [Fact]
    public async Task UpdateCommand_WithActiveGeneration_WaitsBeforeDownloadAndRestart()
    {
        using ApplicationUpdateTestContext context = new();
        context.UpdateServiceMock
            .Setup(service => service.DownloadUpdateAsync(
                It.IsAny<ApplicationUpdate>(),
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.RestartCoordinatorMock
            .Setup(coordinator => coordinator.ApplyAndRestartAsync(
                It.IsAny<ApplicationUpdate>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.StartGeneration();
        await StartMonitoringAsync(context.ViewModel);

        Task updateTask = context.ViewModel.UpdateCommand.ExecuteAsync(null);
        await Task.Yield();

        context.ViewModel.State.Should().Be(ApplicationUpdateState.WaitingForGeneration);
        context.UpdateServiceMock.Verify(
            service => service.DownloadUpdateAsync(
                It.IsAny<ApplicationUpdate>(),
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        context.ActivityTracker.Complete(
            CorrelationId,
            GenerationActivityPhase.GenerationRequest);
        await updateTask;

        context.UpdateServiceMock.Verify(
            service => service.DownloadUpdateAsync(
                It.Is<ApplicationUpdate>(update => update.Version == UpdateVersion),
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.RestartCoordinatorMock.Verify(
            coordinator => coordinator.ApplyAndRestartAsync(
                It.Is<ApplicationUpdate>(update => update.Version == UpdateVersion),
                It.IsAny<CancellationToken>()),
            Times.Once);
        context.ViewModel.IsNotificationOpen.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateLaterCommand_WithAvailableUpdate_HidesNotification()
    {
        using ApplicationUpdateTestContext context = new();
        await StartMonitoringAsync(context.ViewModel);

        context.ViewModel.UpdateLaterCommand.Execute(null);

        context.ViewModel.IsNotificationOpen.Should().BeFalse();
        context.ViewModel.State.Should().Be(ApplicationUpdateState.Hidden);
    }

    [Fact]
    public void LocalizationChangedMessage_WhenSent_RefreshesLocalizationRevision()
    {
        using ApplicationUpdateTestContext context = new();
        int initialRevision = context.ViewModel.LocalizationRevision;

        context.Messenger.Send(new LocalizationChangedMessage());

        context.ViewModel.LocalizationRevision.Should().Be(initialRevision + 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Monitoring_AfterFailedCheckThenSuccessfulRetry_ClearsCheckError(bool updateAvailable)
    {
        using ApplicationUpdateTestContext context = new();
        context.UpdateServiceMock
            .SetupSequence(service => service.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Update source is unavailable."))
            .ReturnsAsync(updateAvailable ? new ApplicationUpdate(UpdateVersion) : null);
        await StartMonitoringAsync(context.ViewModel);
        context.ViewModel.ErrorMessage.Should().Be(
            TestLocalizationTextProvider.Default.Get(UpdateLocalizationKeys.Errors.CheckFailed));

        await context.TickAsync();
        context.Messenger.Send(new LocalizationChangedMessage());

        context.ViewModel.ErrorMessage.Should().BeNull();
        context.ViewModel.HasErrorMessage.Should().BeFalse();
        context.ViewModel.IsNotificationOpen.Should().Be(updateAvailable);
        context.ViewModel.State.Should().Be(updateAvailable
            ? ApplicationUpdateState.Available
            : ApplicationUpdateState.Hidden);
    }

    [Fact]
    public async Task Monitoring_AfterRepeatedCheckFailure_PreservesCheckError()
    {
        using ApplicationUpdateTestContext context = new();
        context.UpdateServiceMock
            .Setup(service => service.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Update source is unavailable."));
        await StartMonitoringAsync(context.ViewModel);

        await context.TickAsync();

        context.ViewModel.ErrorMessage.Should().Be(
            TestLocalizationTextProvider.Default.Get(UpdateLocalizationKeys.Errors.CheckFailed));
        context.ViewModel.HasErrorMessage.Should().BeTrue();
        context.ViewModel.IsNotificationOpen.Should().BeFalse();
        context.UpdateServiceMock.Verify(service => service.CheckForUpdateAsync(
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Monitoring_AfterInstallFailureThenSuccessfulCheck_PreservesInstallError()
    {
        using ApplicationUpdateTestContext context = new();
        context.UpdateServiceMock.Setup(service => service.DownloadUpdateAsync(
                It.IsAny<ApplicationUpdate>(), It.IsAny<IProgress<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Update download failed."));
        await StartMonitoringAsync(context.ViewModel);
        await context.ViewModel.UpdateCommand.ExecuteAsync(null);

        await context.TickAsync();
        context.Messenger.Send(new LocalizationChangedMessage());

        context.ViewModel.ErrorMessage.Should().Be(
            TestLocalizationTextProvider.Default.Get(UpdateLocalizationKeys.Errors.InstallFailed));
        context.ViewModel.HasErrorMessage.Should().BeTrue();
        context.ViewModel.State.Should().Be(ApplicationUpdateState.Available);
        context.ViewModel.UpdateCommand.CanExecute(null).Should().BeTrue();
    }

    private static Task StartMonitoringAsync(ApplicationUpdateViewModel viewModel)
    {
        return viewModel.StartMonitoringCommand.ExecuteAsync(null);
    }

    private sealed class ApplicationUpdateTestContext : IDisposable
    {
        public Mock<IApplicationUpdateService> UpdateServiceMock { get; }
        public Mock<IApplicationUpdateRestartCoordinator> RestartCoordinatorMock { get; }
        public IGenerationActivityTracker ActivityTracker { get; }
        public IMessenger Messenger { get; }
        public ApplicationUpdateViewModel ViewModel { get; }

        private readonly Mock<TimeProvider> _clock = new();
        private Action? _tick;
        private TaskCompletionSource? _nextDelayScheduled;

        public ApplicationUpdateTestContext()
        {
            _clock.Setup(clock => clock.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(),
                    It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
                .Callback<TimerCallback, object?, TimeSpan, TimeSpan>((callback, state, _, _) =>
                {
                    _tick = () => callback(state);
                    _nextDelayScheduled?.TrySetResult();
                })
                .Returns(() => new Mock<ITimer>().Object);
            UpdateServiceMock = new Mock<IApplicationUpdateService>();
            UpdateServiceMock
                .SetupGet(service => service.CanCheckForUpdates)
                .Returns(true);
            UpdateServiceMock
                .Setup(service => service.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApplicationUpdate(UpdateVersion));
            RestartCoordinatorMock = new Mock<IApplicationUpdateRestartCoordinator>();
            ActivityTracker = TestGenerationActivityTrackerFactory.Create();
            Messenger = new WeakReferenceMessenger();
            ViewModel = new ApplicationUpdateViewModel(
                UpdateServiceMock.Object,
                RestartCoordinatorMock.Object,
                ActivityTracker,
                new ImmediateUiThreadDispatcher(),
                Messenger,
                new TestViewModelErrorHandler(),
                TestLocalizationTextProvider.Default,
                TestApiConfiguration.CreateApplicationUpdateOptionsWrapper(),
                _clock.Object);
        }

        public void StartGeneration()
        {
            ActivityTracker.Start(
                CorrelationId,
                GenerationActivityPhase.GenerationRequest);
        }

        public async Task TickAsync()
        {
            Action tick = _tick ?? throw new InvalidOperationException("Update monitoring was not started.");
            _nextDelayScheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            tick();
            await _nextDelayScheduled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }
}
