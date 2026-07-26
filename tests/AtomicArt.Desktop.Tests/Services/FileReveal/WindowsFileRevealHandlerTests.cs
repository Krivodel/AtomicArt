using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using AtomicArt.Desktop.Services;
using AtomicArt.Desktop.Services.FileReveal;
using AtomicArt.Desktop.Tests.TestDoubles;

namespace AtomicArt.Desktop.Tests.Services.FileReveal;

public sealed class WindowsFileRevealHandlerTests
{
    private static readonly string FilePath = Path.GetFullPath(
        Path.Combine("Art", "image.png"));

    [Fact]
    public void Reveal_WithReusableWindow_SelectsFileWithoutOpeningNewWindow()
    {
        RecordingWindowsExplorerWindow window = new();
        RecordingWindowsExplorerWindowLocator locator = new(window);
        RecordingStandardFileRevealer standardRevealer = new();
        WindowsFileRevealHandler handler = CreateHandler(
            locator,
            standardRevealer);

        handler.Reveal(FilePath, FileRevealWindowMode.ReuseExisting);

        locator.DirectoryPath.Should().Be(Path.GetDirectoryName(FilePath));
        window.SelectedFileName.Should().Be(Path.GetFileName(FilePath));
        standardRevealer.CallCount.Should().Be(0);
    }

    [Fact]
    public void Reveal_WithoutReusableWindow_OpensNewWindow()
    {
        RecordingWindowsExplorerWindowLocator locator = new(null);
        RecordingStandardFileRevealer standardRevealer = new();
        WindowsFileRevealHandler handler = CreateHandler(
            locator,
            standardRevealer);

        handler.Reveal(FilePath, FileRevealWindowMode.ReuseExisting);

        locator.CallCount.Should().Be(1);
        standardRevealer.FilePath.Should().Be(FilePath);
    }

    [Fact]
    public void Reveal_WithOpenNewMode_SkipsSearchAndOpensNewWindow()
    {
        RecordingWindowsExplorerWindow window = new();
        RecordingWindowsExplorerWindowLocator locator = new(window);
        RecordingStandardFileRevealer standardRevealer = new();
        WindowsFileRevealHandler handler = CreateHandler(
            locator,
            standardRevealer);

        handler.Reveal(FilePath, FileRevealWindowMode.OpenNew);

        locator.CallCount.Should().Be(0);
        window.SelectedFileName.Should().BeNull();
        standardRevealer.FilePath.Should().Be(FilePath);
    }

    private static WindowsFileRevealHandler CreateHandler(
        IWindowsExplorerWindowLocator locator,
        IStandardFileRevealer standardRevealer)
    {
        return new WindowsFileRevealHandler(
            locator,
            standardRevealer,
            NullLogger<WindowsFileRevealHandler>.Instance);
    }

    private sealed class RecordingWindowsExplorerWindowLocator
        : IWindowsExplorerWindowLocator
    {
        public string? DirectoryPath { get; private set; }
        public int CallCount { get; private set; }

        private readonly IWindowsExplorerWindow? _window;

        public RecordingWindowsExplorerWindowLocator(
            IWindowsExplorerWindow? window)
        {
            _window = window;
        }

        public IWindowsExplorerWindow? Find(string directoryPath)
        {
            CallCount++;
            DirectoryPath = directoryPath;

            return _window;
        }
    }

    private sealed class RecordingWindowsExplorerWindow
        : IWindowsExplorerWindow
    {
        public string? SelectedFileName { get; private set; }

        public void SelectFile(string fileName)
        {
            SelectedFileName = fileName;
        }

        public void Dispose()
        {
        }
    }
}
