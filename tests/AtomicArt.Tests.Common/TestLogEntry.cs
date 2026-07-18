using Microsoft.Extensions.Logging;

namespace AtomicArt.Tests.Common;

public sealed record TestLogEntry(
    LogLevel Level,
    string Message,
    Exception? Exception);
