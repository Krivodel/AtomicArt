using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Logging;

internal sealed class DesktopRollingFileWriter : IDisposable
{
    private const string FileNamePrefix = "atomicart-";
    private const string FileNameSearchPattern = "atomicart-*.log";
    private const string LogRetentionCleanupOperationName = "log retention cleanup";
    private const int MaxMessageLength = 8 * 1024;
    private const int MaxExceptionDepth = 5;
    private const int MaxStackFrameCount = 64;

    private readonly string _directoryPath;
    private readonly LogLevel _minimumLevel;
    private readonly long _maxFileSizeBytes;
    private readonly int _retainedFileCount;
    private readonly int _retentionDays;
    private readonly object _syncRoot = new();
    private StreamWriter? _writer;
    private DateOnly _currentDate;
    private int _currentSequence;
    private long _currentFileSizeBytes;
    private bool _isDisposed;
    private bool _isAvailable;

    public DesktopRollingFileWriter(
        IAtomicArtDataPathProvider pathProvider,
        DesktopFileLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(options);

        _directoryPath = pathProvider.LogsDirectory;
        _minimumLevel = options.MinimumLevel;
        _maxFileSizeBytes = options.MaxFileSizeBytes;
        _retainedFileCount = options.RetainedFileCount;
        _retentionDays = options.RetentionDays;

        try
        {
            pathProvider.EnsureDirectoryExists(_directoryPath);
            _isAvailable = true;
        }
        catch (IOException)
        {
            _isAvailable = false;
        }
        catch (UnauthorizedAccessException)
        {
            _isAvailable = false;
        }
        catch (NotSupportedException)
        {
            _isAvailable = false;
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _isAvailable
            && !_isDisposed
            && logLevel >= _minimumLevel
            && logLevel != LogLevel.None;
    }

    public void Write(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        string message,
        Exception? exception)
    {
        string entry = FormatEntry(logLevel, categoryName, eventId, message, exception);
        int entrySizeBytes = Encoding.UTF8.GetByteCount(entry);

        lock (_syncRoot)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            try
            {
                EnsureWriter(entrySizeBytes);
                _writer?.Write(entry);
                _writer?.Flush();
                _currentFileSizeBytes += entrySizeBytes;
            }
            catch (IOException)
            {
                Disable();
            }
            catch (UnauthorizedAccessException)
            {
                Disable();
            }
            catch (NotSupportedException)
            {
                Disable();
            }
            catch (ObjectDisposedException)
            {
                Disable();
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            try
            {
                _writer?.Dispose();
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _writer = null;
        }
    }

    private static string FormatEntry(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        string message,
        Exception? exception)
    {
        StringBuilder builder = new();
        builder.Append(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" [");
        builder.Append(logLevel);
        builder.Append("] ");
        builder.Append(categoryName);

        if (eventId.Id != 0)
        {
            builder.Append(" EventId=");
            builder.Append(eventId.Id.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(": ");
        builder.AppendLine(NormalizeMessage(message));
        AppendSafeException(builder, exception, 0);

        return builder.ToString();
    }

    private static string NormalizeMessage(string message)
    {
        string normalizedMessage = message
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        return normalizedMessage.Length <= MaxMessageLength
            ? normalizedMessage
            : normalizedMessage[..MaxMessageLength];
    }

    private static void AppendSafeException(
        StringBuilder builder,
        Exception? exception,
        int depth)
    {
        if (exception is null
            || depth >= MaxExceptionDepth)
        {
            return;
        }

        builder.Append("ExceptionType=");
        builder.Append(exception.GetType().FullName);
        builder.Append(" HResult=0x");
        builder.AppendLine(exception.HResult.ToString("X8", CultureInfo.InvariantCulture));

        string? sanitizedMessage = DesktopExceptionMessageSanitizer.Sanitize(exception.Message);

        if (sanitizedMessage is not null)
        {
            builder.Append("ExceptionMessage=");
            builder.AppendLine(sanitizedMessage);
        }

        StackFrame[] frames = new StackTrace(exception, false)
            .GetFrames()
            .Take(MaxStackFrameCount)
            .ToArray();

        foreach (StackFrame frame in frames)
        {
            MethodBase? method = frame.GetMethod();

            if (method is null)
            {
                continue;
            }

            builder.Append("   at ");
            builder.Append(method.DeclaringType?.FullName ?? "<unknown>");
            builder.Append('.');
            builder.AppendLine(method.Name);
        }

        AppendSafeException(builder, exception.InnerException, depth + 1);
    }

    private static int TryParseSequence(string? fileName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(
                fileName.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int sequence))
        {
            return 0;
        }

        return sequence;
    }

    private void EnsureWriter(int entrySizeBytes)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        bool requiresNewFile = _writer is null
            || today != _currentDate
            || (_currentFileSizeBytes > 0
                && _currentFileSizeBytes + entrySizeBytes > _maxFileSizeBytes);

        if (!requiresNewFile)
        {
            return;
        }

        _writer?.Dispose();
        _currentDate = today;
        _currentSequence = FindNextSequence(today);
        string filePath = Path.Combine(
            _directoryPath,
            $"{FileNamePrefix}{today:yyyyMMdd}-{_currentSequence:D3}.log");
        FileStream stream = new(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);
        _writer = new StreamWriter(stream, new UTF8Encoding(false));
        _currentFileSizeBytes = stream.Length;
        DeleteExpiredFiles(filePath);
    }

    private int FindNextSequence(DateOnly date)
    {
        string prefix = $"{FileNamePrefix}{date:yyyyMMdd}-";
        string[] files = Directory.GetFiles(_directoryPath, $"{prefix}*.log");
        int highestSequence = files
            .Select(Path.GetFileNameWithoutExtension)
            .Select(fileName => TryParseSequence(fileName, prefix))
            .DefaultIfEmpty(0)
            .Max();

        if (files.Length == 0)
        {
            return 1;
        }

        string latestPath = files
            .OrderBy(path => path, StringComparer.Ordinal)
            .Last();
        FileInfo latestFile = new(latestPath);

        return latestFile.Length < _maxFileSizeBytes
            ? highestSequence
            : highestSequence + 1;
    }

    private void DeleteExpiredFiles(string currentFilePath)
    {
        string[] retainedCandidates = Directory
            .GetFiles(_directoryPath, FileNameSearchPattern)
            .Where(path => !string.Equals(path, currentFilePath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        DateTime retentionCutoffUtc = DateTime.UtcNow.AddDays(-_retentionDays);
        string[] expiredFiles = retainedCandidates
            .Where(path => File.GetLastWriteTimeUtc(path) < retentionCutoffUtc)
            .Concat(retainedCandidates.Skip(_retainedFileCount - 1))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (string expiredFile in expiredFiles)
        {
            TryDeleteExpiredFile(expiredFile);
        }
    }

    private void TryDeleteExpiredFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            WriteInternalFailure(LogRetentionCleanupOperationName, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteInternalFailure(LogRetentionCleanupOperationName, ex);
        }
        catch (NotSupportedException ex)
        {
            WriteInternalFailure(LogRetentionCleanupOperationName, ex);
        }
    }

    private void WriteInternalFailure(string operation, Exception exception)
    {
        _writer?.WriteLine(
            "{0:O} [Warning] AtomicArt.Desktop.Logging: {1} failed. ExceptionType={2}",
            DateTimeOffset.Now,
            operation,
            exception.GetType().FullName);
        _writer?.Flush();
    }

    private void Disable()
    {
        _isAvailable = false;

        try
        {
            _writer?.Dispose();
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _writer = null;
    }
}
