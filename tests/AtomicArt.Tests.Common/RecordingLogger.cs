using Microsoft.Extensions.Logging;

namespace AtomicArt.Tests.Common;

public sealed class RecordingLogger<TCategory> : ILogger<TCategory>
{
    public IReadOnlyList<TestLogEntry> Entries => _entries.ToList();
    public IReadOnlyList<string> Messages => _entries
        .Select(entry => entry.Message)
        .ToList();
    public IReadOnlyList<string> WarningMessages => _entries
        .Where(entry => entry.Level == LogLevel.Warning)
        .Select(entry => entry.Message)
        .ToList();
    public int WarningCount => _entries.Count(entry => entry.Level == LogLevel.Warning);

    private readonly List<TestLogEntry> _entries = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return TestNullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _entries.Add(new TestLogEntry(
            logLevel,
            formatter(state, exception),
            exception));
    }
}
