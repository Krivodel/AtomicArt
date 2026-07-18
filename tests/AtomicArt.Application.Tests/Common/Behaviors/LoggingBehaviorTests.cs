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
        RecordingLogger<LoggingBehavior<TestRequest, TestResponse>> logger = new();
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

    [Fact]
    public async Task Handle_WithFailedResult_LogsResultStatusAndErrorCode()
    {
        const string errorCode = "ERR-TEST-001";
        RecordingLogger<LoggingBehavior<ResultRequest, Result<TestPayload>>> logger = new();
        LoggingBehavior<ResultRequest, Result<TestPayload>> behavior = new(logger);
        ResultRequest request = new();
        RequestHandlerDelegate<Result<TestPayload>> next = _ =>
            Task.FromResult(Result<TestPayload>.ValidationError(errorCode, "Test validation failed."));

        await behavior.Handle(request, next, CancellationToken.None);

        string logText = string.Join(Environment.NewLine, logger.Messages);
        logText.Should().Contain("completed with status ValidationError");
        logText.Should().Contain(errorCode);
    }

    [Fact]
    public async Task Handle_WithSuccessfulResult_LogsSuccessStatusWithoutErrorCode()
    {
        RecordingLogger<LoggingBehavior<ResultRequest, Result<TestPayload>>> logger = new();
        LoggingBehavior<ResultRequest, Result<TestPayload>> behavior = new(logger);
        ResultRequest request = new();
        RequestHandlerDelegate<Result<TestPayload>> next = _ =>
            Task.FromResult(Result<TestPayload>.Success(new TestPayload()));

        await behavior.Handle(request, next, CancellationToken.None);

        string logText = string.Join(Environment.NewLine, logger.Messages);
        logText.Should().Contain("completed with status Success");
        logText.Should().NotContain("and error code");
    }

    [Fact]
    public async Task Handle_WithOrdinaryResponse_LogsSuccessWithoutInspectingNamedProperties()
    {
        RecordingLogger<LoggingBehavior<TestRequest, TestResponse>> logger = new();
        LoggingBehavior<TestRequest, TestResponse> behavior = new(logger);
        TestRequest request = new("safe prompt");
        RequestHandlerDelegate<TestResponse> next = _ =>
            Task.FromResult(new TestResponse("Custom", "ERR-IGNORED"));

        await behavior.Handle(request, next, CancellationToken.None);

        string logText = string.Join(Environment.NewLine, logger.Messages);
        logText.Should().Contain("completed with status Success");
        logText.Should().NotContain("Custom");
        logText.Should().NotContain("ERR-IGNORED");
    }

    private sealed record TestRequest(string Prompt) : IRequest<TestResponse>;

    private sealed record TestResponse(string Status, string? ErrorCode);

    private sealed record ResultRequest : IRequest<Result<TestPayload>>;

    private sealed record TestPayload;
}
