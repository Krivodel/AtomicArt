using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

using AtomicArt.Api.Controllers;
using AtomicArt.Api.Generation;
using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Commands.CreateStreamingGeneration;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.Controllers;

public sealed class GenerationsControllerTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BatchId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ItemId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime StartedAtUtc =
        new(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CompletedAtUtc =
        new(2026, 7, 23, 10, 0, 5, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_WhenConcurrencyLimitIsReached_ReturnsNonRetryableTooManyRequests()
    {
        Mock<IMediator> mediator = new();
        GenerationsController controller = CreateController(
            mediator.Object,
            new FullGenerationRequestConcurrencyLimiter());

        IActionResult result = await controller.CreateAsync(
            CancellationToken.None);

        ObjectResult objectResult = result
            .Should()
            .BeOfType<ObjectResult>()
            .Subject;
        objectResult.StatusCode.Should().Be(
            StatusCodes.Status429TooManyRequests);
        ProblemDetails problemDetails = objectResult.Value
            .Should()
            .BeOfType<ProblemDetails>()
            .Subject;
        problemDetails.Extensions[
                GenerationApiRoutes.ProblemDetailsErrorCodeExtensionName]
            .Should()
            .Be(GenerationProtocolErrorCodes.ConcurrencyLimitReached);
        problemDetails.Extensions[
                GenerationApiRoutes.ProblemDetailsRetryableExtensionName]
            .Should()
            .Be(false);
        mediator.Verify(
            currentMediator => currentMediator.Send(
                It.IsAny<CreateStreamingGenerationCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GenerationRoute_UsesVersionTwo()
    {
        GenerationApiRoutes.Generations.Should().Be(
            "api/v2/generations");
    }

    [Fact]
    public async Task CreateAsync_WithPreparedAttempt_WritesProviderAndFinalMetadataParts()
    {
        Mock<IMediator> mediator = new();
        StreamingGenerationAttempt attempt = CreateAttempt();
        mediator
            .Setup(currentMediator => currentMediator.Send(
                It.IsAny<CreateStreamingGenerationCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(GenerationAttemptPreparation.Success(attempt));
        GenerationsController controller = CreateController(
            mediator.Object,
            new AvailableGenerationRequestConcurrencyLimiter());
        using MultipartFormDataContent requestContent =
            CreateRequestContent();
        await using Stream requestBody =
            await requestContent.ReadAsStreamAsync();
        controller.HttpContext.Request.ContentType =
            requestContent.Headers.ContentType?.ToString();
        controller.HttpContext.Request.Body = requestBody;
        using MemoryStream responseBody = new();
        controller.HttpContext.Response.Body = responseBody;

        IActionResult result = await controller.CreateAsync(
            CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        responseBody.Position = 0L;
        string responseText = await new StreamReader(
                responseBody,
                Encoding.UTF8)
            .ReadToEndAsync();
        responseText.Should().Contain(
            $"name=\"{GenerationApiRoutes.ProviderResponsePartName}\"");
        responseText.Should().Contain("\"data\":\"iVBORw==\"");
        responseText.Should().Contain(
            $"name=\"{GenerationApiRoutes.GenerationMetadataPartName}\"");
        responseText.Should().Contain(
            $"\"logicalGenerationId\":\"{LogicalGenerationId}\"");
        responseText.Should().Contain("\"status\":\"Generated\"");
    }

    private static GenerationsController CreateController(
        IMediator mediator,
        IGenerationRequestConcurrencyLimiter concurrencyLimiter)
    {
        DefaultHttpContext httpContext = new();
        GenerationsController controller = new(
            mediator,
            concurrencyLimiter,
            new MultipartGenerationRequestReader(),
            new GenerationStreamingResponseWriter(
                NullLogger<GenerationStreamingResponseWriter>.Instance),
            NullLogger<GenerationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    private static StreamingGenerationAttempt CreateAttempt()
    {
        GenerationModelMetadataDto model =
            ApiModelMetadataTestCatalog.LoadNanoBanana2Metadata();
        StreamingImageGenerationRequest request = new(
            LogicalGenerationId,
            1,
            model.Id,
            "Create an image",
            "16:9",
            "2K",
            1.0,
            "low",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Array.Empty<IGenerationAttachmentSource>());

        return new StreamingGenerationAttempt(
            new TestProviderGenerationStream(),
            new GenerationUsagePriceCalculator(),
            new FixedDateTimeProvider(CompletedAtUtc),
            request,
            model,
            GenerationProviderIds.Google,
            BatchId,
            ItemId,
            StartedAtUtc);
    }

    private static MultipartFormDataContent CreateRequestContent()
    {
        GenerationRequestMetadataDto metadata = new(
            LogicalGenerationId,
            1,
            ApiModelMetadataTestCatalog.NanoBanana2ModelId,
            "Create an image",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Array.Empty<GenerationAttachmentMetadataDto>());
        MultipartFormDataContent content = new();
        content.Add(
            new StringContent(
                JsonSerializer.Serialize(
                    metadata,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Encoding.UTF8,
                "application/json"),
            GenerationApiRoutes.MetadataPartName);

        return content;
    }

    private sealed class FullGenerationRequestConcurrencyLimiter
        : IGenerationRequestConcurrencyLimiter
    {
        public IDisposable? TryAcquire()
        {
            return null;
        }
    }

    private sealed class AvailableGenerationRequestConcurrencyLimiter
        : IGenerationRequestConcurrencyLimiter
    {
        public IDisposable? TryAcquire()
        {
            return new TestLease();
        }
    }

    private sealed class TestProviderGenerationStream
        : IProviderGenerationStream
    {
        public string ContentType => "application/json";
        public ProviderGenerationSummary? Summary { get; private set; }

        public async Task CopyToAsync(
            Stream destination,
            long maximumBytes,
            CancellationToken ct)
        {
            byte[] response = """
                {"status":"completed","output":[{"mime_type":"image/png","data":"iVBORw=="}]}
                """u8.ToArray();
            await destination.WriteAsync(response, ct);

            Summary = new ProviderGenerationSummary(
                "completed",
                1,
                new List<string>
                {
                    GenerationImageContentTypes.Png
                },
                null);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow { get; }

        public FixedDateTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }
    }

    private sealed class TestLease : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
