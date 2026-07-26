using System.Globalization;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.FileReveal;

internal sealed class WindowsExplorerWindowLocator
    : IWindowsExplorerWindowLocator
{
    private const string ShellApplicationProgrammaticId = "Shell.Application";

    private readonly ILogger<WindowsExplorerWindowLocator> _logger;

    public WindowsExplorerWindowLocator(
        ILogger<WindowsExplorerWindowLocator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IWindowsExplorerWindow? Find(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        object? shellApplication = null;
        object? shellWindows = null;

        try
        {
            Type? shellApplicationType = Type.GetTypeFromProgID(
                ShellApplicationProgrammaticId);

            if (shellApplicationType is null)
            {
                return null;
            }

            shellApplication = Activator.CreateInstance(shellApplicationType);

            if (shellApplication is null)
            {
                return null;
            }

            shellWindows = WindowsShellAutomation.InvokeMethod(
                shellApplication,
                "Windows");

            if (shellWindows is null)
            {
                return null;
            }

            object? windowCountValue =
                WindowsShellAutomation.GetProperty(shellWindows, "Count");
            int windowCount = Convert.ToInt32(
                windowCountValue,
                CultureInfo.InvariantCulture);

            for (int index = 0; index < windowCount; index++)
            {
                IWindowsExplorerWindow? window = GetMatchingWindow(
                    shellWindows,
                    index,
                    directoryPath);

                if (window is not null)
                {
                    return window;
                }
            }
        }
        catch (Exception ex) when (WindowsShellAutomation.IsAutomationException(ex))
        {
            _logger.LogDebug(
                ex,
                "Failed to inspect existing File Explorer windows.");
        }
        finally
        {
            WindowsShellAutomation.Release(shellWindows);
            WindowsShellAutomation.Release(shellApplication);
        }

        return null;
    }

    private static IWindowsExplorerWindow? GetMatchingWindow(
        object shellWindows,
        int index,
        string directoryPath)
    {
        object? window = null;
        object? document = null;
        object? folder = null;
        object? folderSelf = null;

        try
        {
            window = WindowsShellAutomation.InvokeMethod(
                shellWindows,
                "Item",
                [index]);

            if (window is null)
            {
                return null;
            }

            document = WindowsShellAutomation.GetProperty(window, "Document");
            folder = document is null
                ? null
                : WindowsShellAutomation.GetProperty(document, "Folder");

            if (document is null || folder is null)
            {
                return null;
            }

            folderSelf = WindowsShellAutomation.GetProperty(folder, "Self");
            string? openDirectoryPath = folderSelf is null
                ? null
                : WindowsShellAutomation.GetProperty(folderSelf, "Path") as string;

            if (!AreSameDirectory(openDirectoryPath, directoryPath))
            {
                return null;
            }

            WindowsExplorerWindow result = new(window, document, folder);
            window = null;
            document = null;
            folder = null;

            return result;
        }
        catch (Exception ex) when (IsWindowInspectionFailure(ex))
        {
            return null;
        }
        finally
        {
            WindowsShellAutomation.Release(folderSelf);
            WindowsShellAutomation.Release(folder);
            WindowsShellAutomation.Release(document);
            WindowsShellAutomation.Release(window);
        }
    }

    private static bool IsWindowInspectionFailure(Exception exception)
    {
        return WindowsShellAutomation.IsAutomationException(exception)
            || exception is ArgumentException
            or NotSupportedException;
    }

    private static bool AreSameDirectory(
        string? firstPath,
        string secondPath)
    {
        if (string.IsNullOrWhiteSpace(firstPath))
        {
            return false;
        }

        string normalizedFirstPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(firstPath));
        string normalizedSecondPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(secondPath));

        return string.Equals(
            normalizedFirstPath,
            normalizedSecondPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
