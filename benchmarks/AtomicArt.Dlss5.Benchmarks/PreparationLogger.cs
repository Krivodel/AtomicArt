using Microsoft.Extensions.Logging;

using AtomicArt.Desktop.Services.Dlss5;

namespace AtomicArt.Dlss5.Benchmarks;

internal sealed class PreparationLogger : ILogger<Dlss5NativeEngine>
{
    public int WorkerStarts => Volatile.Read(ref _workerStarts);

    private int _workerStarts;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
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
        string message = formatter(state, exception);
        if (message.StartsWith("Started hidden DLSS 5 worker process", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _workerStarts);
        }

        if ((message.Contains("setup completed", StringComparison.Ordinal))
            || (message.Contains("total prewarm", StringComparison.Ordinal))
            || (logLevel >= LogLevel.Warning))
        {
            Console.WriteLine(message);
        }
    }
}
