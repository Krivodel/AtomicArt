using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Paths;

namespace AtomicArt.Desktop.Services.Logging;

public sealed class DesktopFileLoggerProvider :
    ILoggerProvider,
    ISupportExternalScope,
    IDataRootLogRelocationService
{
    private readonly ConcurrentDictionary<string, DesktopFileLogger> _loggers;
    private readonly DesktopRollingFileWriter _writer;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public DesktopFileLoggerProvider(
        IAtomicArtDataPathProvider pathProvider,
        DesktopFileLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(options);

        _loggers = new ConcurrentDictionary<string, DesktopFileLogger>(StringComparer.Ordinal);
        _writer = new DesktopRollingFileWriter(pathProvider, options);
    }

    public ILogger CreateLogger(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        return _loggers.GetOrAdd(
            categoryName,
            category => new DesktopFileLogger(category, _writer, () => _scopeProvider));
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider ?? throw new ArgumentNullException(nameof(scopeProvider));
    }

    public void Pause()
    {
        _writer.Pause();
    }

    public void Resume(IAtomicArtDataPathProvider pathProvider)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);

        _writer.Resume(pathProvider);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
