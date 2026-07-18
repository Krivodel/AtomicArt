using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Updates;
using AtomicArt.Desktop.Tests.Services.Generation;
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
        context.ViewModel.UpdateActionText.Should().Be(UiStrings.UpdateWaitAndInstall);
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

    private static Task StartMonitoringAsync(ApplicationUpdateViewModel viewModel)
    {
        return viewModel.StartMonitoringCommand.ExecuteAsync(null);
    }

    private sealed class ApplicationUpdateTestContext : IDisposable
    {
        public Mock<IApplicationUpdateService> UpdateServiceMock { get; }
        public Mock<IApplicationUpdateRestartCoordinator> RestartCoordinatorMock { get; }
        public IGenerationActivityTracker ActivityTracker { get; }
        public ApplicationUpdateViewModel ViewModel { get; }

        public ApplicationUpdateTestContext()
        {
            UpdateServiceMock = new Mock<IApplicationUpdateService>();
            UpdateServiceMock
                .SetupGet(service => service.CanCheckForUpdates)
                .Returns(true);
            UpdateServiceMock
                .Setup(service => service.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApplicationUpdate(UpdateVersion));
            RestartCoordinatorMock = new Mock<IApplicationUpdateRestartCoordinator>();
            ActivityTracker = TestGenerationActivityTrackerFactory.Create();
            ViewModel = new ApplicationUpdateViewModel(
                UpdateServiceMock.Object,
                RestartCoordinatorMock.Object,
                ActivityTracker,
                new ImmediateUiThreadDispatcher(),
                new TestViewModelErrorHandler());
        }

        public void StartGeneration()
        {
            ActivityTracker.Start(
                CorrelationId,
                GenerationActivityPhase.GenerationRequest);
        }

        public void Dispose()
        {
            ViewModel.Dispose();
        }
    }
}
