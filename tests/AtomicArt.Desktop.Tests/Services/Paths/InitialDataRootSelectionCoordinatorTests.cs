using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Tests.TestDoubles;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class InitialDataRootSelectionCoordinatorTests
{
    [Fact]
    public async Task OfferAsync_WhenConfirmed_CompletesOfferAndChangesDirectory()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(InitialDataRootSelectionCoordinatorTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);
            RecordingDialogService dialogService = new()
            {
                ConfirmationResult = true
            };
            InitialDataRootSelectionCoordinator coordinator = CreateCoordinator(
                store,
                rootDirectory,
                dialogService);
            int changeDirectoryCallCount = 0;

            await coordinator.OfferAsync(
                () =>
                {
                    changeDirectoryCallCount++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            LocalizedConfirmationDialogRequest request = dialogService
                .ConfirmationRequests
                .Single();
            request.TitleLocalizationKey.Should().Be(
                SettingsLocalizationKeys.DataRoot.Label);
            request.MessageLocalizationKey.Should().Be(
                SettingsLocalizationKeys.DataRoot.InitialSelectionMessage);
            request.ConfirmActionLocalizationKey.Should().Be(
                CommonLocalizationKeys.Yes);
            request.CancelActionLocalizationKey.Should().Be(
                CommonLocalizationKeys.NotNow);
            request.Kind.Should().Be(ConfirmationDialogKind.Standard);
            request.BackgroundClickBehavior.Should().Be(
                ConfirmationDialogBackgroundClickBehavior.Ignore);
            request.MessageArguments.Should().Equal(rootDirectory);
            changeDirectoryCallCount.Should().Be(1);
            store.ShouldOfferInitialRootDirectorySelection().Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task OfferAsync_WhenDeclined_CompletesOfferWithoutChangingDirectory()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(InitialDataRootSelectionCoordinatorTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);
            RecordingDialogService dialogService = new()
            {
                ConfirmationResult = false
            };
            InitialDataRootSelectionCoordinator coordinator = CreateCoordinator(
                store,
                rootDirectory,
                dialogService);
            int changeDirectoryCallCount = 0;

            await coordinator.OfferAsync(
                () =>
                {
                    changeDirectoryCallCount++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            changeDirectoryCallCount.Should().Be(0);
            store.ShouldOfferInitialRootDirectorySelection().Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task OfferAsync_WhenDialogIsCanceled_LeavesOfferPending()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(InitialDataRootSelectionCoordinatorTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);
            Mock<IDialogService> dialogServiceMock = new();
            dialogServiceMock
                .Setup(service => service.ShowConfirmationAsync(
                    It.IsAny<LocalizedConfirmationDialogRequest>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            InitialDataRootSelectionCoordinator coordinator = CreateCoordinator(
                store,
                rootDirectory,
                dialogServiceMock.Object);

            Func<Task> act = async () => await coordinator.OfferAsync(
                () => Task.CompletedTask,
                CancellationToken.None);

            await act.Should().ThrowAsync<OperationCanceledException>();
            store.ShouldOfferInitialRootDirectorySelection().Should().BeTrue();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    [Fact]
    public async Task OfferAsync_WhenDirectoryChangeFails_LeavesOfferCompleted()
    {
        string bootstrapDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(InitialDataRootSelectionCoordinatorTests));
        string rootDirectory = string.Concat(bootstrapDirectory, "-data");

        try
        {
            AtomicArtDataRootBootstrapStore store = new(
                bootstrapDirectory,
                rootDirectory);
            RecordingDialogService dialogService = new()
            {
                ConfirmationResult = true
            };
            InitialDataRootSelectionCoordinator coordinator = CreateCoordinator(
                store,
                rootDirectory,
                dialogService);

            Func<Task> act = async () => await coordinator.OfferAsync(
                () => throw new InvalidOperationException("Migration failed."),
                CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            store.ShouldOfferInitialRootDirectorySelection().Should().BeFalse();
        }
        finally
        {
            TestDirectories.DeleteIfExists(bootstrapDirectory);
            TestDirectories.DeleteIfExists(rootDirectory);
        }
    }

    private static InitialDataRootSelectionCoordinator CreateCoordinator(
        AtomicArtDataRootBootstrapStore store,
        string rootDirectory,
        IDialogService dialogService)
    {
        Mock<IUiThreadDispatcher> uiThreadDispatcherMock = new();
        uiThreadDispatcherMock
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task> action, CancellationToken _) => action());

        return new InitialDataRootSelectionCoordinator(
            store,
            new AtomicArtDataPathProvider(rootDirectory),
            dialogService,
            uiThreadDispatcherMock.Object,
            NullLogger<InitialDataRootSelectionCoordinator>.Instance);
    }
}
