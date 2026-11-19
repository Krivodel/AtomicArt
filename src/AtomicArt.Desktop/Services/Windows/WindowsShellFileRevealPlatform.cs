using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using Pica.Viewer.Services;

namespace AtomicArt.Desktop.Services.Windows;

internal sealed class WindowsShellFileRevealPlatform : IFileRevealPlatform
{
    private const uint UseFolderAssociationFallback = 0x00000008;
    private const uint QueryAssociatedExecutable = 2;
    private const int AssociationQuerySucceeded = 0;

    private readonly IFileRevealPlatform _explorerReveal;
    private readonly Func<string, string?> _getDefaultFolderExecutable;
    private readonly Action<ProcessStartInfo> _startProcess;

    public WindowsShellFileRevealPlatform(IFileRevealPlatform explorerReveal)
        : this(explorerReveal, GetDefaultFolderExecutable, StartProcess)
    {
    }

    internal WindowsShellFileRevealPlatform(
        IFileRevealPlatform explorerReveal,
        Func<string, string?> getDefaultFolderExecutable,
        Action<ProcessStartInfo> startProcess)
    {
        _explorerReveal = explorerReveal ?? throw new ArgumentNullException(nameof(explorerReveal));
        _getDefaultFolderExecutable = getDefaultFolderExecutable
            ?? throw new ArgumentNullException(nameof(getDefaultFolderExecutable));
        _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
    }

    public Task RevealAsync(string filePath, FileRevealWindowMode windowMode, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(nameof(windowMode), windowMode, "Unsupported file reveal window mode.");
        }

        string fullPath = Path.GetFullPath(filePath);
        string directoryPath = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The file must have a parent directory.", nameof(filePath));
        string? executablePath = _getDefaultFolderExecutable(directoryPath);
        string explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        if (string.Equals(executablePath, explorerPath, StringComparison.OrdinalIgnoreCase))
        {
            return _explorerReveal.RevealAsync(fullPath, windowMode, ct);
        }

        ct.ThrowIfCancellationRequested();
        _startProcess(CreateStartInfo(executablePath, fullPath, directoryPath));
        return Task.CompletedTask;
    }

    private static ProcessStartInfo CreateStartInfo(string? executablePath, string filePath, string directoryPath)
    {
        if (executablePath is not null && Path.IsPathFullyQualified(executablePath) && IsSigmaExecutable(executablePath))
        {
            ProcessStartInfo startInfo = new(executablePath) { UseShellExecute = false };
            startInfo.ArgumentList.Add(filePath);
            return startInfo;
        }

        return new ProcessStartInfo(directoryPath) { UseShellExecute = true };
    }

    private static bool IsSigmaExecutable(string executablePath)
    {
        string fileName = Path.GetFileName(executablePath);
        return string.Equals(fileName, "sigma-file-manager-launcher.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "sigma-file-manager.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDefaultFolderExecutable(string directoryPath)
    {
        string association = Path.GetPathRoot(directoryPath) == directoryPath ? "Drive" : "Directory";
        uint capacity = 0;
        _ = AssocQueryString(UseFolderAssociationFallback, QueryAssociatedExecutable, association, null, null, ref capacity);
        if (capacity == 0)
        {
            return null;
        }

        StringBuilder executable = new(checked((int)capacity));
        int result = AssocQueryString(
            UseFolderAssociationFallback, QueryAssociatedExecutable, association, null, executable, ref capacity);
        return result == AssociationQuerySucceeded ? executable.ToString() : null;
    }

    private static void StartProcess(ProcessStartInfo startInfo)
    {
        using Process? process = Process.Start(startInfo);
    }

    [DllImport("shlwapi.dll", EntryPoint = "AssocQueryStringW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int AssocQueryString(
        uint flags,
        uint query,
        string association,
        string? verb,
        StringBuilder? output,
        ref uint capacity);
}
