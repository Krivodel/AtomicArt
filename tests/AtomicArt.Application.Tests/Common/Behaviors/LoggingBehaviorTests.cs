using Microsoft.Extensions.Logging;

using FluentAssertions;
using MediatR;
using Xunit;

using AtomicArt.Application.Common.Behaviors;

namespace AtomicArt.Application.Tests.Common.Behaviors;

public sealed class LoggingBehaviorTests
{
    [Fact]
    public async Task Handle_WhenHandlerFails_LogsCompletionAndDurationWithoutRequestContent()
    {
        const string secretPrompt = "private prompt that must not be logged";
        RecordingLogger logger = new();
        LoggingBehavior<TestRequest, TestResponse> behavior = new(logger);
        TestRequest request = new(secretPrompt);
        RequestHandlerDelegate<TestResponse> next = _ =>
            throw new InvalidOperationException("Safe test failure.");

        Func<Task> act = () => behavior.Handle(
            request,
            next,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        string logText = string.Join(Environment.NewLine, logger.Messages);
        logText.Should().Contain(nameof(TestRequest));
        logText.Should().Contain("failed after");
        logText.Should().Contain("ms");
        logText.Should().NotContain(secretPrompt);
    }

    private sealed record TestRequest(string Prompt) : IRequest<TestResponse>;

    private sealed record TestResponse(string Status, string? ErrorCode);

    private sealed class RecordingLogger : ILogger<LoggingBehavior<TestRequest, TestResponse>>
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
