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
        Mock<IApplicationUpdateService> updateServiceMock = CreateUpdateServiceMock();
        IGenerationActivityTracker activityTracker = TestGenerationActivityTrackerFactory.Create();
        activityTracker.Start(CorrelationId, GenerationActivityPhase.GenerationRequest);
        using ApplicationUpdateViewModel viewModel = CreateViewModel(
            updateServiceMock.Object,
            Mock.Of<IApplicationUpdateRestartCoordinator>(),
            activityTracker);

        await viewModel.StartMonitoringCommand.ExecuteAsync(null);

        viewModel.IsNotificationOpen.Should().BeTrue();
        viewModel.State.Should().Be(ApplicationUpdateState.Available);
        viewModel.UpdateActionText.Should().Be(UiStrings.UpdateWaitAndInstall);
        viewModel.Message.Should().Contain(UpdateVersion);
    }

    [Fact]
    public async Task UpdateCommand_WithActiveGeneration_WaitsBeforeDownloadAndRestart()
    {
        Mock<IApplicationUpdateService> updateServiceMock = CreateUpdateServiceMock();
        updateServiceMock
            .Setup(service => service.DownloadUpdateAsync(
                It.IsAny<ApplicationUpdate>(),
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IApplicationUpdateRestartCoordinator> restartCoordinatorMock = new();
        restartCoordinatorMock
            .Setup(coordinator => coordinator.ApplyAndRestartAsync(
                It.IsAny<ApplicationUpdate>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        IGenerationActivityTracker activityTracker = TestGenerationActivityTrackerFactory.Create();
        activityTracker.Start(CorrelationId, GenerationActivityPhase.GenerationRequest);
        using ApplicationUpdateViewModel viewModel = CreateViewModel(
            updateServiceMock.Object,
            restartCoordinatorMock.Object,
            activityTracker);
        await viewModel.StartMonitoringCommand.ExecuteAsync(null);

        Task updateTask = viewModel.UpdateCommand.ExecuteAsync(null);
        await Task.Yield();

        viewModel.State.Should().Be(ApplicationUpdateState.WaitingForGeneration);
        updateServiceMock.Verify(
            service => service.DownloadUpdateAsync(
                It.IsAny<ApplicationUpdate>(),
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        activityTracker.Complete(CorrelationId, GenerationActivityPhase.GenerationRequest);
        await updateTask;

        updateServiceMock.Verify(
            service => service.DownloadUpdateAsync(
                It.Is<ApplicationUpdate>(update => update.Version == UpdateVersion),
                It.IsAny<IProgress<int>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        restartCoordinatorMock.Verify(
            coordinator => coordinator.ApplyAndRestartAsync(
                It.Is<ApplicationUpdate>(update => update.Version == UpdateVersion),
                It.IsAny<CancellationToken>()),
            Times.Once);
        viewModel.IsNotificationOpen.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateLaterCommand_WithAvailableUpdate_HidesNotification()
    {
        Mock<IApplicationUpdateService> updateServiceMock = CreateUpdateServiceMock();
        using ApplicationUpdateViewModel viewModel = CreateViewModel(
            updateServiceMock.Object,
            Mock.Of<IApplicationUpdateRestartCoordinator>(),
            TestGenerationActivityTrackerFactory.Create());
        await viewModel.StartMonitoringCommand.ExecuteAsync(null);

        viewModel.UpdateLaterCommand.Execute(null);

        viewModel.IsNotificationOpen.Should().BeFalse();
        viewModel.State.Should().Be(ApplicationUpdateState.Hidden);
    }

    private static Mock<IApplicationUpdateService> CreateUpdateServiceMock()
    {
        Mock<IApplicationUpdateService> updateServiceMock = new();
        updateServiceMock
            .SetupGet(service => service.CanCheckForUpdates)
            .Returns(true);
        updateServiceMock
            .Setup(service => service.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplicationUpdate(UpdateVersion));

        return updateServiceMock;
    }

    private static ApplicationUpdateViewModel CreateViewModel(
        IApplicationUpdateService updateService,
        IApplicationUpdateRestartCoordinator restartCoordinator,
        IGenerationActivityTracker activityTracker)
    {
        return new ApplicationUpdateViewModel(
            updateService,
            restartCoordinator,
            activityTracker,
            new ImmediateUiThreadDispatcher(),
            new TestViewModelErrorHandler());
    }
}
