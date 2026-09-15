using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

namespace AtomicArt.Desktop.Services.Dlss5;

internal static class Dlss5WorkerDirectoryLifecycle
{
    internal const string DirectoryPrefix = "dlss5-worker-";

    private const string OwnerFileName = ".owner";
    private const string OwnerFileVersion = "1";
    private const int WorkerIdentifierLength = 32;
    private const int OwnerFileValueCount = 3;
    private const int OwnerFileVersionIndex = 0;
    private const int OwnerFileProcessIdIndex = 1;
    private const int OwnerFileStartTimeIndex = 2;

    internal static void MarkOwnedByCurrentProcess(string workerRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerRoot);

        using Process process = Process.GetCurrentProcess();
        Dlss5WorkerDirectoryLifecycle.MarkOwnedByProcess(workerRoot, process);
    }

    internal static void MarkOwnedByProcess(string workerRoot, Process process)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerRoot);
        ArgumentNullException.ThrowIfNull(process);

        string content = string.Create(
            CultureInfo.InvariantCulture,
            $"{OwnerFileVersion}\n{process.Id}\n{process.StartTime.ToUniversalTime().Ticks}");
        File.WriteAllText(
            Path.Combine(workerRoot, OwnerFileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    internal static int DeleteStaleWorkerDirectories(string runtimeDirectory, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        if (!Directory.Exists(runtimeDirectory))
        {
            return 0;
        }

        int deletedDirectoryCount = 0;
        try
        {
            foreach (string workerRoot in Directory.EnumerateDirectories(runtimeDirectory))
            {
                if ((!Dlss5WorkerDirectoryLifecycle.IsCanonicalWorkerRoot(runtimeDirectory, workerRoot))
                    || (Dlss5WorkerDirectoryLifecycle.IsOwnerProcessRunning(workerRoot, logger)))
                {
                    continue;
                }

                if (Dlss5WorkerDirectoryLifecycle.TryDeleteWorkerRoot(workerRoot, logger))
                {
                    deletedDirectoryCount++;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(
                exception,
                "Unable to enumerate stale DLSS 5 worker directories in {RuntimeDirectory}.",
                runtimeDirectory);
        }

        return deletedDirectoryCount;
    }

    internal static void TryDeleteWorkerDirectory(
        string runtimeDirectory,
        string? workerDirectory,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(workerDirectory))
        {
            return;
        }

        string fullDirectory;
        try
        {
            fullDirectory = Path.GetFullPath(workerDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return;
        }

        string? workerRoot = Path.GetDirectoryName(fullDirectory);
        if ((workerRoot is not null) && (!Directory.Exists(workerRoot)))
        {
            return;
        }

        if ((!string.Equals(Path.GetFileName(fullDirectory), "host", StringComparison.OrdinalIgnoreCase))
            || (workerRoot is null)
            || (!Dlss5WorkerDirectoryLifecycle.IsCanonicalWorkerRoot(runtimeDirectory, workerRoot)))
        {
            logger.LogWarning(
                "Refusing to delete DLSS 5 worker directory outside the runtime workers root: {WorkerDirectory}.",
                fullDirectory);
            return;
        }

        Dlss5WorkerDirectoryLifecycle.TryDeleteWorkerRoot(workerRoot, logger);
    }

    private static bool IsCanonicalWorkerRoot(string runtimeDirectory, string workerRoot)
    {
        string fullRuntimeDirectory;
        string fullWorkerRoot;
        try
        {
            fullRuntimeDirectory = Path.GetFullPath(runtimeDirectory);
            fullWorkerRoot = Path.GetFullPath(workerRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }

        string? workerParent = Path.GetDirectoryName(fullWorkerRoot);
        string workerName = Path.GetFileName(fullWorkerRoot);
        if ((!string.Equals(workerParent, fullRuntimeDirectory, StringComparison.OrdinalIgnoreCase))
            || (!workerName.StartsWith(DirectoryPrefix, StringComparison.OrdinalIgnoreCase))
            || (workerName.Length != DirectoryPrefix.Length + WorkerIdentifierLength))
        {
            return false;
        }

        try
        {
            if ((File.GetAttributes(fullWorkerRoot) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        ReadOnlySpan<char> identifier = workerName.AsSpan(DirectoryPrefix.Length);
        foreach (char character in identifier)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOwnerProcessRunning(string workerRoot, ILogger logger)
    {
        string ownerFilePath = Path.Combine(workerRoot, OwnerFileName);
        if (!File.Exists(ownerFilePath))
        {
            return false;
        }

        try
        {
            string[] values = File.ReadAllLines(ownerFilePath);
            if ((values.Length != OwnerFileValueCount)
                || (!string.Equals(
                    values[OwnerFileVersionIndex],
                    OwnerFileVersion,
                    StringComparison.Ordinal))
                || (!int.TryParse(
                    values[OwnerFileProcessIdIndex],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int processId))
                || (!long.TryParse(
                    values[OwnerFileStartTimeIndex],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long startTimeUtcTicks)))
            {
                return true;
            }

            using Process process = Process.GetProcessById(processId);

            return (!process.HasExited)
                && (process.StartTime.ToUniversalTime().Ticks == startTimeUtcTicks);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            logger.LogDebug(
                exception,
                "Unable to verify the owner of DLSS 5 worker directory {WorkerDirectory}; the directory was preserved.",
                workerRoot);
            return true;
        }
    }

    private static bool TryDeleteWorkerRoot(string workerRoot, ILogger logger)
    {
        try
        {
            if (Directory.Exists(workerRoot))
            {
                Directory.Delete(workerRoot, recursive: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(
                exception,
                "Unable to remove DLSS 5 worker directory {WorkerDirectory}; it will be retried when DLSS 5 is next initialized.",
                workerRoot);
            return false;
        }
    }
}
