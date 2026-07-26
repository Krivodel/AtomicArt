using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.FileReveal;

internal sealed class WindowsFileRevealHandler
{
    private readonly IWindowsExplorerWindowLocator _windowLocator;
    private readonly IStandardFileRevealer _standardFileRevealer;
    private readonly ILogger<WindowsFileRevealHandler> _logger;

    public WindowsFileRevealHandler(
        IWindowsExplorerWindowLocator windowLocator,
        IStandardFileRevealer standardFileRevealer,
        ILogger<WindowsFileRevealHandler> logger)
    {
        _windowLocator = windowLocator
            ?? throw new ArgumentNullException(nameof(windowLocator));
        _standardFileRevealer = standardFileRevealer
            ?? throw new ArgumentNullException(nameof(standardFileRevealer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Reveal(string filePath, FileRevealWindowMode windowMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (windowMode == FileRevealWindowMode.ReuseExisting
            && TryRevealInExistingWindow(filePath))
        {
            return;
        }

        _standardFileRevealer.Reveal(filePath);
    }

    private bool TryRevealInExistingWindow(string filePath)
    {
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException(
                "The image directory could not be determined.");
        using IWindowsExplorerWindow? window =
            _windowLocator.Find(directoryPath);

        if (window is null)
        {
            return false;
        }

        try
        {
            window.SelectFile(Path.GetFileName(filePath));

            return true;
        }
        catch (Exception ex) when (IsSelectionFailure(ex))
        {
            _logger.LogDebug(
                ex,
                "Failed to select a file in an existing File Explorer window.");

            return false;
        }
    }

    private static bool IsSelectionFailure(Exception exception)
    {
        return exception is COMException
            or InvalidCastException
            or InvalidOperationException
            or MissingMemberException
            or TargetInvocationException;
    }
}
