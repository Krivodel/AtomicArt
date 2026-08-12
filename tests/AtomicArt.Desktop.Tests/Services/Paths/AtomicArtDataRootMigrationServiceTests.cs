using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Gallery;
using AtomicArt.Desktop.Services.Generation;
using AtomicArt.Desktop.Services.Logging;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.Services.State;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.Services.Paths;

public sealed class AtomicArtDataRootMigrationServiceTests
{
    [Fact]
    public async Task MigrateAsync_WithValidDestination_MovesAllDataAndSwitchesRoot()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootMigrationServiceTests));
        string sourceDirectory = Path.Combine(testDirectory, "Source");
        string destinationDirectory = Path.Combine(testDirectory, "Destination");
        string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");
        byte[] content = [1, 2, 3, 4, 5];

        try
        {
            string sourceFile = CreateSourceFile(sourceDirectory, content);
            Directory.CreateDirectory(destinationDirectory);
            AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            await bootstrapStore.SaveRootDirectoryAsync(
                sourceDirectory,
                CancellationToken.None);
            Mock<IDataRootMigrationTarget> targetMock = CreateTargetMock();
            Mock<IApplicationStateFlushService> flushServiceMock = new();
            flushServiceMock
                .Setup(service => service.FlushAsync(
                    targetMock.Object,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Mock<IDataRootLogRelocationService> logRelocationMock = new();
            AtomicArtDataRootMigrationService service = CreateService(
                pathProvider,
                bootstrapStore,
                targetMock,
                flushServiceMock.Object,
                logRelocationMock.Object);
            List<DataRootMigrationProgress> reportedProgress = [];
            Mock<IProgress<DataRootMigrationProgress>> progressMock = new();
            progressMock
                .Setup(progress => progress.Report(It.IsAny<DataRootMigrationProgress>()))
                .Callback<DataRootMigrationProgress>(reportedProgress.Add);

            await service.MigrateAsync(
                destinationDirectory,
                progressMock.Object,
                CancellationToken.None);

            string destinationFile = Path.Combine(
                destinationDirectory,
                Path.GetRelativePath(sourceDirectory, sourceFile));
            File.Exists(destinationFile).Should().BeTrue();
            File.ReadAllBytes(destinationFile).Should().Equal(content);
            Directory.Exists(sourceDirectory).Should().BeFalse();
            pathProvider.RootDirectory.Should().Be(Path.GetFullPath(destinationDirectory));
            bootstrapStore.LoadRootDirectory().Should().Be(
                Path.GetFullPath(destinationDirectory));
            reportedProgress.Should().NotBeEmpty();
            reportedProgress[^1].Stage.Should().Be(
                DataRootMigrationProgressStage.Completed);
            reportedProgress[^1].Percentage.Should().Be(100);
            targetMock.Verify(
                target => target.RebaseDataRootAsync(
                    Path.GetFullPath(sourceDirectory),
                    Path.GetFullPath(destinationDirectory),
                    CancellationToken.None),
                Times.Once);
            logRelocationMock.Verify(service => service.Pause(), Times.Once);
            logRelocationMock.Verify(
                service => service.Resume(pathProvider),
                Times.Once);
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    [Fact]
    public async Task MigrateAsync_WithLockedInstanceCoordinationFile_MovesManagedData()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootMigrationServiceTests));
        string sourceDirectory = Path.Combine(testDirectory, "Source");
        string destinationDirectory = Path.Combine(testDirectory, "Destination");
        string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");
        byte[] content = [1, 2, 3];

        try
        {
            string sourceFile = CreateSourceFile(sourceDirectory, content);
            string coordinationDirectory = Path.Combine(
                sourceDirectory,
                AtomicArtPathNames.SingleInstanceCoordinationDirectory);
            Directory.CreateDirectory(coordinationDirectory);
            string lockFilePath = Path.Combine(coordinationDirectory, "instance.lock");
            using FileStream lockStream = new(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            Directory.CreateDirectory(destinationDirectory);
            AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            await bootstrapStore.SaveRootDirectoryAsync(
                sourceDirectory,
                CancellationToken.None);
            Mock<IDataRootMigrationTarget> targetMock = CreateTargetMock();
            Mock<IApplicationStateFlushService> flushServiceMock = new();
            flushServiceMock
                .Setup(service => service.FlushAsync(
                    targetMock.Object,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            AtomicArtDataRootMigrationService service = CreateService(
                pathProvider,
                bootstrapStore,
                targetMock,
                flushServiceMock.Object,
                Mock.Of<IDataRootLogRelocationService>());
            Mock<IProgress<DataRootMigrationProgress>> progressMock = new();

            await service.MigrateAsync(
                destinationDirectory,
                progressMock.Object,
                CancellationToken.None);

            string destinationFile = Path.Combine(
                destinationDirectory,
                Path.GetRelativePath(sourceDirectory, sourceFile));
            File.ReadAllBytes(destinationFile).Should().Equal(content);
            File.Exists(lockFilePath).Should().BeTrue();
            Directory.Exists(Path.Combine(
                destinationDirectory,
                AtomicArtPathNames.SingleInstanceCoordinationDirectory)).Should().BeFalse();
            pathProvider.RootDirectory.Should().Be(Path.GetFullPath(destinationDirectory));
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    [Fact]
    public async Task MigrateAsync_WhenTargetSavesState_AccessesNewRootWithoutDeadlock()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootMigrationServiceTests));
        string sourceDirectory = Path.Combine(testDirectory, "Source");
        string destinationDirectory = Path.Combine(testDirectory, "Destination");
        string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");

        try
        {
            CreateSourceFile(sourceDirectory, new byte[] { 1, 2, 3 });
            Directory.CreateDirectory(destinationDirectory);
            AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            await bootstrapStore.SaveRootDirectoryAsync(
                sourceDirectory,
                CancellationToken.None);
            DataRootAccessCoordinator accessCoordinator = new();
            Mock<IDataRootMigrationTarget> targetMock = new();
            targetMock
                .Setup(target => target.RebaseDataRootAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    using CancellationTokenSource timeoutSource =
                        new(TimeSpan.FromSeconds(2));
                    using DataRootAccessLease accessLease =
                        await accessCoordinator.AcquireAccessAsync(timeoutSource.Token);
                });
            Mock<IApplicationStateFlushService> flushServiceMock = new();
            flushServiceMock
                .Setup(service => service.FlushAsync(
                    targetMock.Object,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            AtomicArtDataRootMigrationService service = CreateService(
                pathProvider,
                bootstrapStore,
                targetMock,
                flushServiceMock.Object,
                Mock.Of<IDataRootLogRelocationService>(),
                accessCoordinator);
            Mock<IProgress<DataRootMigrationProgress>> progressMock = new();

            Func<Task> act = () => service.MigrateAsync(
                destinationDirectory,
                progressMock.Object,
                CancellationToken.None);

            await act.Should().NotThrowAsync();
            pathProvider.RootDirectory.Should().Be(Path.GetFullPath(destinationDirectory));
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    [Fact]
    public async Task MigrateAsync_WithPendingRecovery_DoesNotOverwriteJournal()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootMigrationServiceTests));
        string sourceDirectory = Path.Combine(testDirectory, "Source");
        string destinationDirectory = Path.Combine(testDirectory, "Destination");
        string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");

        try
        {
            CreateSourceFile(sourceDirectory, new byte[] { 1, 2, 3 });
            Directory.CreateDirectory(destinationDirectory);
            AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            await bootstrapStore.SaveRootDirectoryAsync(
                sourceDirectory,
                CancellationToken.None);
            DataRootMigrationJournalStore journalStore = new(bootstrapStore);
            DataRootMigrationJournal pendingJournal = new()
            {
                SourceRootDirectory = Path.Combine(testDirectory, "Previous"),
                DestinationRootDirectory = sourceDirectory,
                Stage = DataRootMigrationStage.CleaningSource,
                Directories = Array.Empty<string>(),
                Files = Array.Empty<DataRootMigrationFile>()
            };
            await journalStore.SaveAsync(pendingJournal, CancellationToken.None);
            Mock<IDataRootMigrationTarget> targetMock = CreateTargetMock();
            AtomicArtDataRootMigrationService service = CreateService(
                pathProvider,
                bootstrapStore,
                targetMock,
                Mock.Of<IApplicationStateFlushService>(),
                Mock.Of<IDataRootLogRelocationService>());
            Mock<IProgress<DataRootMigrationProgress>> progressMock = new();

            Func<Task> act = () => service.MigrateAsync(
                destinationDirectory,
                progressMock.Object,
                CancellationToken.None);

            await act.Should().ThrowAsync<DataRootMigrationCleanupException>();
            journalStore.Load().Should().BeEquivalentTo(pendingJournal);
            pathProvider.RootDirectory.Should().Be(Path.GetFullPath(sourceDirectory));
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    [Fact]
    public async Task MigrateAsync_WhenCanceledDuringCopy_KeepsSourceAndCleansDestination()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootMigrationServiceTests));
        string sourceDirectory = Path.Combine(testDirectory, "Source");
        string destinationDirectory = Path.Combine(testDirectory, "Destination");
        string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");
        byte[] content = new byte[3 * 1024 * 1024];

        try
        {
            CreateSourceFile(sourceDirectory, content);
            Directory.CreateDirectory(destinationDirectory);
            AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            await bootstrapStore.SaveRootDirectoryAsync(
                sourceDirectory,
                CancellationToken.None);
            Mock<IDataRootMigrationTarget> targetMock = CreateTargetMock();
            Mock<IApplicationStateFlushService> flushServiceMock = new();
            flushServiceMock
                .Setup(service => service.FlushAsync(
                    targetMock.Object,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            AtomicArtDataRootMigrationService service = CreateService(
                pathProvider,
                bootstrapStore,
                targetMock,
                flushServiceMock.Object,
                Mock.Of<IDataRootLogRelocationService>());
            using CancellationTokenSource cancellationSource = new();
            Mock<IProgress<DataRootMigrationProgress>> progressMock = new();
            progressMock
                .Setup(progress => progress.Report(It.IsAny<DataRootMigrationProgress>()))
                .Callback<DataRootMigrationProgress>(progress =>
                {
                    if (progress.Stage == DataRootMigrationProgressStage.Copying
                        && progress.CompletedBytes > 0)
                    {
                        cancellationSource.Cancel();
                    }
                });

            Func<Task> act = () => service.MigrateAsync(
                destinationDirectory,
                progressMock.Object,
                cancellationSource.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            Directory.Exists(sourceDirectory).Should().BeTrue();
            Directory.EnumerateFileSystemEntries(destinationDirectory).Should().BeEmpty();
            pathProvider.RootDirectory.Should().Be(Path.GetFullPath(sourceDirectory));
            bootstrapStore.LoadRootDirectory().Should().Be(Path.GetFullPath(sourceDirectory));
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    [Fact]
    public async Task MigrateAsync_WhenSourceChangesAfterSwitch_KeepsNewRootAndReportsPendingCleanup()
    {
        string testDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(AtomicArtDataRootMigrationServiceTests));
        string sourceDirectory = Path.Combine(testDirectory, "Source");
        string destinationDirectory = Path.Combine(testDirectory, "Destination");
        string bootstrapDirectory = Path.Combine(testDirectory, "Bootstrap");
        byte[] content = [7, 8, 9];

        try
        {
            string sourceFile = CreateSourceFile(sourceDirectory, content);
            Directory.CreateDirectory(destinationDirectory);
            AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
            AtomicArtDataRootBootstrapStore bootstrapStore = new(bootstrapDirectory);
            await bootstrapStore.SaveRootDirectoryAsync(
                sourceDirectory,
                CancellationToken.None);
            Mock<IDataRootMigrationTarget> targetMock = new();
            targetMock
                .Setup(target => target.RebaseDataRootAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => File.WriteAllText(sourceFile, "changed"))
                .Returns(Task.CompletedTask);
            Mock<IApplicationStateFlushService> flushServiceMock = new();
            flushServiceMock
                .Setup(service => service.FlushAsync(
                    targetMock.Object,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            AtomicArtDataRootMigrationService service = CreateService(
                pathProvider,
                bootstrapStore,
                targetMock,
                flushServiceMock.Object,
                Mock.Of<IDataRootLogRelocationService>());
            Mock<IProgress<DataRootMigrationProgress>> progressMock = new();

            Func<Task> act = () => service.MigrateAsync(
                destinationDirectory,
                progressMock.Object,
                CancellationToken.None);

            await act.Should().ThrowAsync<DataRootMigrationCleanupException>();
            pathProvider.RootDirectory.Should().Be(Path.GetFullPath(destinationDirectory));
            bootstrapStore.LoadRootDirectory().Should().Be(
                Path.GetFullPath(destinationDirectory));
            File.Exists(sourceFile).Should().BeTrue();
            File.Exists(Path.Combine(destinationDirectory, "Art", "image.png"))
                .Should()
                .BeTrue();
        }
        finally
        {
            TestDirectories.DeleteIfExists(testDirectory);
        }
    }

    private static Mock<IDataRootMigrationTarget> CreateTargetMock()
    {
        Mock<IDataRootMigrationTarget> targetMock = new();
        targetMock
            .Setup(target => target.RebaseDataRootAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return targetMock;
    }

    private static AtomicArtDataRootMigrationService CreateService(
        AtomicArtDataPathProvider pathProvider,
        AtomicArtDataRootBootstrapStore bootstrapStore,
        Mock<IDataRootMigrationTarget> targetMock,
        IApplicationStateFlushService flushService,
        IDataRootLogRelocationService logRelocationService,
        IDataRootAccessCoordinator? accessCoordinator = null)
    {
        DataRootMigrationTargetAttachmentService targetAttachmentService = new();
        targetAttachmentService.Attach(targetMock.Object);
        Mock<IGenerationActivityTracker> activityTrackerMock = new();
        activityTrackerMock
            .Setup(tracker => tracker.WaitUntilIdleAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IDataRootViewerPreparationService> viewerPreparationMock = new();
        viewerPreparationMock
            .Setup(service => service.CloseAllAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IUiThreadDispatcher> uiThreadDispatcherMock = new();
        uiThreadDispatcherMock
            .Setup(dispatcher => dispatcher.InvokeAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((action, _) => action());
        accessCoordinator ??= new DataRootAccessCoordinator();

        return new AtomicArtDataRootMigrationService(
            pathProvider,
            pathProvider,
            bootstrapStore,
            new DataRootMigrationJournalStore(bootstrapStore),
            new DataRootMigrationPlanner(),
            new DataRootFileTransfer(
                TestApiConfiguration.CreateStorageOptionsWrapper()),
            new GenerationAdmissionGate(),
            activityTrackerMock.Object,
            accessCoordinator,
            flushService,
            viewerPreparationMock.Object,
            targetAttachmentService,
            logRelocationService,
            uiThreadDispatcherMock.Object,
            NullLogger<AtomicArtDataRootMigrationService>.Instance);
    }

    private static string CreateSourceFile(
        string sourceDirectory,
        byte[] content)
    {
        string artDirectory = Path.Combine(sourceDirectory, "Art");
        Directory.CreateDirectory(artDirectory);
        string sourceFile = Path.Combine(artDirectory, "image.png");
        File.WriteAllBytes(sourceFile, content);

        return sourceFile;
    }
}
