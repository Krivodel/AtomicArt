using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using FluentAssertions;
using Xunit;

using AtomicArt.Api.Middleware;

namespace AtomicArt.Api.Tests.Middleware;

public sealed class ApiRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CompletedRequest_LogsMethodStatusAndDurationWithoutPath()
    {
        RecordingLogger logger = new();
        ApiRequestLoggingMiddleware middleware = new(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;

                return Task.CompletedTask;
            },
            logger);
        DefaultHttpContext context = new()
        {
            TraceIdentifier = "trace-1",
            Request =
            {
                Method = HttpMethods.Post,
                Path = "/private/prompt/value"
            }
        };
        
        await middleware.InvokeAsync(context);

        string logText = string.Join(Environment.NewLine, logger.Messages);
        logText.Should().Contain("trace-1");
        logText.Should().Contain("POST");
        logText.Should().Contain("204");
        logText.Should().Contain("ms");
        logText.Should().NotContain("private");
        logText.Should().NotContain("prompt");
    }

    private sealed class RecordingLogger : ILogger<ApiRequestLoggingMiddleware>
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullDisposable.Instance;
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

            _messages.Add(formatter(state, exception));
        }

        private sealed class NullDisposable : IDisposable
        {
            public static NullDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
