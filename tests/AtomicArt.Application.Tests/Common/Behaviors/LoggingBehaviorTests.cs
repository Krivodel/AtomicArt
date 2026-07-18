using FluentAssertions;
using MediatR;
using Xunit;

using AtomicArt.Application.Common.Behaviors;
using AtomicArt.Application.Common.Models;
using AtomicArt.Tests.Common;

namespace AtomicArt.Application.Tests.Common.Behaviors;

public sealed class LoggingBehaviorTests
{
    [Fact]
    public async Task Handle_WhenHandlerFails_LogsCompletionAndDurationWithoutRequestContent()
    {
        const string secretPrompt = "private prompt that must not be logged";
        LoggingBehavior<TestRequest, TestResponse> behavior =
            CreateBehavior(out RecordingLogger<LoggingBehavior<TestRequest, TestResponse>> logger);
        TestRequest request = new(secretPrompt);
        RequestHandlerDelegate<TestResponse> next = _ =>
            throw new InvalidOperationException("Safe test failure.");

        Func<Task> act = () => behavior.Handle(
            request,
            next,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        string logText = GetLogText(logger);
        logText.Should().Contain(nameof(TestRequest));
        logText.Should().Contain("failed after");
        logText.Should().Contain("ms");
        logText.Should().NotContain(secretPrompt);
    }

    [Fact]
    public async Task Handle_WithFailedResult_LogsResultStatusAndErrorCode()
    {
        const string errorCode = "ERR-TEST-001";
        Result<TestPayload> result =
            Result<TestPayload>.ValidationError(errorCode, "Test validation failed.");

        string logText = await HandleResultAsync(result);

        logText.Should().Contain("completed with status ValidationError");
        logText.Should().Contain(errorCode);
    }

    [Fact]
    public async Task Handle_WithSuccessfulResult_LogsSuccessStatusWithoutErrorCode()
    {
        Result<TestPayload> result = Result<TestPayload>.Success(new TestPayload());

        string logText = await HandleResultAsync(result);

        logText.Should().Contain("completed with status Success");
        logText.Should().NotContain("and error code");
    }

    [Fact]
    public async Task Handle_WithOrdinaryResponse_LogsSuccessWithoutInspectingNamedProperties()
    {
        LoggingBehavior<TestRequest, TestResponse> behavior =
            CreateBehavior(out RecordingLogger<LoggingBehavior<TestRequest, TestResponse>> logger);
        TestRequest request = new("safe prompt");
        RequestHandlerDelegate<TestResponse> next = _ =>
            Task.FromResult(new TestResponse("Custom", "ERR-IGNORED"));

        await behavior.Handle(request, next, CancellationToken.None);

        string logText = GetLogText(logger);
        logText.Should().Contain("completed with status Success");
        logText.Should().NotContain("Custom");
        logText.Should().NotContain("ERR-IGNORED");
    }

    private static LoggingBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>(
        out RecordingLogger<LoggingBehavior<TRequest, TResponse>> logger)
        where TRequest : notnull, IRequest<TResponse>
    {
        logger = new RecordingLogger<LoggingBehavior<TRequest, TResponse>>();

        return new LoggingBehavior<TRequest, TResponse>(logger);
    }

    private static string GetLogText<TCategory>(RecordingLogger<TCategory> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return string.Join(Environment.NewLine, logger.Messages);
    }

    private static async Task<string> HandleResultAsync(Result<TestPayload> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        LoggingBehavior<ResultRequest, Result<TestPayload>> behavior =
            CreateBehavior(
                out RecordingLogger<
                    LoggingBehavior<ResultRequest, Result<TestPayload>>> logger);
        ResultRequest request = new();
        RequestHandlerDelegate<Result<TestPayload>> next = _ => Task.FromResult(result);

        await behavior.Handle(request, next, CancellationToken.None);

        return GetLogText(logger);
    }

    private sealed record TestRequest(string Prompt) : IRequest<TestResponse>;

    private sealed record TestResponse(string Status, string? ErrorCode);

    private sealed record ResultRequest : IRequest<Result<TestPayload>>;

    private sealed record TestPayload;
}
