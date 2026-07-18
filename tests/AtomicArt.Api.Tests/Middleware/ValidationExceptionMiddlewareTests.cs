using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Xunit;

using AtomicArt.Api.Middleware;

namespace AtomicArt.Api.Tests.Middleware;

public sealed class ValidationExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenValidationException_ReturnsBadRequestProblemDetails()
    {
        DefaultHttpContext context = new()
        {
            Request =
            {
                Path = "/api/v1/generations"
            },
            Response =
            {
                Body = new MemoryStream()
            }
        };
        List<ValidationFailure> failures =
        [
            new("Prompt", "Промпт обязателен.")
        ];
        RequestDelegate next = _ => throw new ValidationException(failures);
        ValidationExceptionMiddleware middleware = new(
            next,
            NullLogger<ValidationExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/problem+json");
        string responseBody = await ReadResponseBodyAsync(context);
        using JsonDocument document = JsonDocument.Parse(responseBody);
        JsonElement root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest);
        root.GetProperty("title").GetString().Should().Be("Ошибка валидации запроса.");
        root.GetProperty("detail").GetString().Should().Be("Запрос не прошёл проверку.");
        root.GetProperty("fields").EnumerateArray()
            .Select(field => field.GetString())
            .Should()
            .ContainSingle("Prompt");
    }

    private static async Task<string> ReadResponseBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using StreamReader reader = new(context.Response.Body);

        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
