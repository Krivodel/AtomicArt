using System.Diagnostics;

using FluentAssertions;
using Moq;
using Pica.Viewer.Services;
using Xunit;

using AtomicArt.Desktop.Services.Windows;

namespace AtomicArt.Desktop.Tests.Services.Windows;

public sealed class WindowsShellFileRevealPlatformTests
{
    [Theory]
    [InlineData("sigma-file-manager-launcher.exe")]
    [InlineData("SIGMA-FILE-MANAGER.EXE")]
    public async Task RevealAsync_WithSigmaAsDefault_PassesImageToRegisteredExecutable(string executableName)
    {
        string filePath = Path.GetFullPath(Path.Combine("Галерея с пробелами", "Картинка & 1.png"));
        string executablePath = Path.GetFullPath(Path.Combine("File managers", executableName));
        Mock<IFileRevealPlatform> explorerReveal = new(MockBehavior.Strict);
        ProcessStartInfo? launch = null;
        WindowsShellFileRevealPlatform platform = new(explorerReveal.Object, _ => executablePath, info => launch = info);

        await platform.RevealAsync(filePath, FileRevealWindowMode.ReuseExisting, CancellationToken.None);

        launch.Should().NotBeNull();
        launch!.FileName.Should().Be(executablePath);
        launch.UseShellExecute.Should().BeFalse();
        launch.ArgumentList.Should().Equal(filePath);
    }

    [Theory]
    [InlineData(FileRevealWindowMode.ReuseExisting)]
    [InlineData(FileRevealWindowMode.OpenNew)]
    public async Task RevealAsync_WithAnotherDefaultManager_OpensParentThroughShell(FileRevealWindowMode mode)
    {
        string directoryPath = Path.GetFullPath("Gallery with spaces");
        string filePath = Path.Combine(directoryPath, "Image.png");
        string executablePath = Path.GetFullPath(Path.Combine("File managers", "Default.exe"));
        Mock<IFileRevealPlatform> explorerReveal = new(MockBehavior.Strict);
        ProcessStartInfo? launch = null;
        WindowsShellFileRevealPlatform platform = new(
            explorerReveal.Object,
            _ => executablePath,
            startInfo => launch = startInfo);

        await platform.RevealAsync(filePath, mode, CancellationToken.None);

        launch.Should().NotBeNull();
        launch!.FileName.Should().Be(directoryPath);
        launch.UseShellExecute.Should().BeTrue();
        launch.Verb.Should().BeEmpty();
        launch.Arguments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(FileRevealWindowMode.ReuseExisting)]
    [InlineData(FileRevealWindowMode.OpenNew)]
    public async Task RevealAsync_WithWindowsExplorer_KeepsNativeSelection(FileRevealWindowMode mode)
    {
        string filePath = Path.GetFullPath(Path.Combine("Gallery", "Image.png"));
        string executablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        Mock<IFileRevealPlatform> explorerReveal = new(MockBehavior.Strict);
        explorerReveal.Setup(platform => platform.RevealAsync(filePath, mode, CancellationToken.None))
            .Returns(Task.CompletedTask);
        WindowsShellFileRevealPlatform platform = new(
            explorerReveal.Object,
            _ => executablePath,
            _ => throw new InvalidOperationException("The shell must not open a second window."));

        await platform.RevealAsync(filePath, mode, CancellationToken.None);

        explorerReveal.Verify(service => service.RevealAsync(filePath, mode, CancellationToken.None), Times.Once);
    }
}
