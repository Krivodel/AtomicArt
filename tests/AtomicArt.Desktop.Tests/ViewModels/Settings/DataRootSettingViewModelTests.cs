using FluentAssertions;
using Moq;
using Xunit;

using AtomicArt.Desktop.Resources;
using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.Paths;
using AtomicArt.Desktop.ViewModels.Settings;
using AtomicArt.Tests.Common;

namespace AtomicArt.Desktop.Tests.ViewModels.Settings;

public sealed class DataRootSettingViewModelTests
{
    [Fact]
    public async Task ChangeDirectoryCommand_WithSelectedFolder_UpdatesPathAndProgress()
    {
        string sourceDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(DataRootSettingViewModelTests));
        string destinationDirectory = string.Concat(sourceDirectory, "-destination");
        AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
        Mock<IFolderPickerService> folderPickerMock = new();
        folderPickerMock
            .Setup(service => service.PickFolderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(destinationDirectory);
        TaskCompletionSource continueMigrationSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IAtomicArtDataRootMigrationService> migrationServiceMock = new();
        migrationServiceMock
            .Setup(service => service.MigrateAsync(
                destinationDirectory,
                It.IsAny<IProgress<DataRootMigrationProgress>>(),
                It.IsAny<CancellationToken>()))
                .Returns<string, IProgress<DataRootMigrationProgress>, CancellationToken>(
                async (_, progress, _) =>
                {
                    progress.Report(new DataRootMigrationProgress
                    {
                        Stage = DataRootMigrationProgressStage.Preparing,
                        CompletedBytes = 0,
                        TotalBytes = 0,
                        CompletedFiles = 0,
                        TotalFiles = 0
                    });
                    await continueMigrationSource.Task;
                    progress.Report(new DataRootMigrationProgress
                    {
                        Stage = DataRootMigrationProgressStage.Completed,
                        CompletedBytes = 10,
                        TotalBytes = 10,
                        CompletedFiles = 1,
                        TotalFiles = 1
                    });
                    pathProvider.SwitchRootDirectory(destinationDirectory);
                });
        DataRootSettingViewModel viewModel = new(
            new DataRootSettingDefinition(),
            folderPickerMock.Object,
            migrationServiceMock.Object,
            pathProvider,
            Mock.Of<IViewModelErrorHandler>(),
            TestLocalizationTextProvider.Default);

        using CancellationTokenSource waitCancellation = new(TimeSpan.FromSeconds(2));
        Task commandTask = viewModel.ChangeDirectoryCommand.ExecuteAsync(null);
        await AsyncTestWaiter.WaitForConditionAsync(
            () => viewModel.IsProgressIndeterminate,
            waitCancellation.Token);
        continueMigrationSource.SetResult();
        await commandTask;
        await AsyncTestWaiter.WaitForConditionAsync(
            () => viewModel.ProgressText == TestLocalizationTextProvider.Default.Get(SettingsLocalizationKeys.DataRoot.Completed),
            waitCancellation.Token);

        viewModel.Value.Should().Be(Path.GetFullPath(destinationDirectory));
        viewModel.ProgressPercentage.Should().Be(100);
        viewModel.IsProgressIndeterminate.Should().BeFalse();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ChangeDirectoryCommand_WhenPickerIsCanceled_DoesNotStartMigration()
    {
        string sourceDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(DataRootSettingViewModelTests));
        AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
        Mock<IFolderPickerService> folderPickerMock = new();
        folderPickerMock
            .Setup(service => service.PickFolderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        Mock<IAtomicArtDataRootMigrationService> migrationServiceMock = new();
        DataRootSettingViewModel viewModel = new(
            new DataRootSettingDefinition(),
            folderPickerMock.Object,
            migrationServiceMock.Object,
            pathProvider,
            Mock.Of<IViewModelErrorHandler>(),
            TestLocalizationTextProvider.Default);

        await viewModel.ChangeDirectoryCommand.ExecuteAsync(null);

        migrationServiceMock.Verify(
            service => service.MigrateAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<DataRootMigrationProgress>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        viewModel.Value.Should().Be(Path.GetFullPath(sourceDirectory));
        viewModel.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeDirectoryCommand_WhenMigrationFails_ShowsSafeError()
    {
        string sourceDirectory = TestDirectories.GetUniqueDirectoryPath(
            typeof(DataRootSettingViewModelTests));
        string destinationDirectory = string.Concat(sourceDirectory, "-destination");
        AtomicArtDataPathProvider pathProvider = new(sourceDirectory);
        Mock<IFolderPickerService> folderPickerMock = new();
        folderPickerMock
            .Setup(service => service.PickFolderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(destinationDirectory);
        DataRootMigrationException exception = new(new IOException("Failure"));
        Mock<IAtomicArtDataRootMigrationService> migrationServiceMock = new();
        migrationServiceMock
            .Setup(service => service.MigrateAsync(
                destinationDirectory,
                It.IsAny<IProgress<DataRootMigrationProgress>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        Mock<IViewModelErrorHandler> errorHandlerMock = new();
        errorHandlerMock
            .Setup(handler => handler.GetUserMessage(exception))
            .Returns(TestLocalizationTextProvider.Default.Get(SettingsLocalizationKeys.DataRoot.MigrationFailed));
        DataRootSettingViewModel viewModel = new(
            new DataRootSettingDefinition(),
            folderPickerMock.Object,
            migrationServiceMock.Object,
            pathProvider,
            errorHandlerMock.Object,
            TestLocalizationTextProvider.Default);

        await viewModel.ChangeDirectoryCommand.ExecuteAsync(null);

        errorHandlerMock.Verify(
            handler => handler.Log(exception, "ChangeDirectoryAsync"),
            Times.Once);
        viewModel.ErrorMessage.Should().Be(TestLocalizationTextProvider.Default.Get(SettingsLocalizationKeys.DataRoot.MigrationFailed));
        viewModel.Value.Should().Be(Path.GetFullPath(sourceDirectory));
        viewModel.IsLoading.Should().BeFalse();
    }
}
