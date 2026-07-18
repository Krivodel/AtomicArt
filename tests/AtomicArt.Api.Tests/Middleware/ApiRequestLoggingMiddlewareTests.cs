using Microsoft.AspNetCore.Http;

using FluentAssertions;
using Xunit;

using AtomicArt.Api.Middleware;
using AtomicArt.Tests.Common;

namespace AtomicArt.Api.Tests.Middleware;

public sealed class ApiRequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CompletedRequest_LogsMethodStatusAndDurationWithoutPath()
    {
        RecordingLogger<ApiRequestLoggingMiddleware> logger = new();
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
}
