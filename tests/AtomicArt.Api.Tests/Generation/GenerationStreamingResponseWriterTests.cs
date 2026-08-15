using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using FluentAssertions;
using Xunit;

using AtomicArt.Api.Generation;
using AtomicArt.Application.Common.Interfaces;
using AtomicArt.Application.Features.Generation.Interfaces;
using AtomicArt.Application.Features.Generation.Models;
using AtomicArt.Application.Features.Generation.Services;
using AtomicArt.Contracts.Generation;
using AtomicArt.Tests.Common;
using AtomicArt.Tests.Common.Generation;

namespace AtomicArt.Api.Tests.Generation;

public sealed class GenerationStreamingResponseWriterTests
{
    private static readonly Guid LogicalGenerationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime StartedAtUtc =
        new(2026, 7, 24, 19, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CompletedAtUtc =
        new(2026, 7, 24, 19, 0, 5, DateTimeKind.Utc);

    [Fact]
    public async Task WriteAsync_WithProviderStreamingFailure_LogsSafeFailureDetails()
    {
        RecordingLogger<StreamingGenerationAttempt> attemptLogger = new();
        RecordingLogger<GenerationStreamingResponseWriter> writerLogger = new();
        await using StreamingGenerationAttempt attempt = CreateAttempt(
            attemptLogger);
        GenerationStreamingResponseWriter writer = new(writerLogger);
        DefaultHttpContext httpContext = new();
        using MemoryStream responseBody = new();
        httpContext.Response.Body = responseBody;

        IActionResult result = await writer.WriteAsync(
            httpContext,
            attempt,
            CancellationToken.None);

        result.Should().BeOfType<EmptyResult>();
        attemptLogger.WarningMessages.Should().ContainSingle(message =>
            message.Contains("InvalidResponse", StringComparison.Ordinal)
            && message.Contains(
                GenerationProviderFailureErrorCodes.InvalidResponse,
                StringComparison.Ordinal)
            && message.Contains("retryable False", StringComparison.Ordinal));
        attemptLogger.Entries.Should().ContainSingle()
            .Which.Exception.Should()
            .BeOfType<ImageGenerationProviderException>();
        writerLogger.WarningMessages.Should().ContainSingle(message =>
            message.Contains(
                LogicalGenerationId.ToString(),
                StringComparison.Ordinal)
            && message.Contains(
                GenerationProviderFailureErrorCodes.InvalidResponse,
                StringComparison.Ordinal)
            && message.Contains("retryable False", StringComparison.Ordinal));
    }

    private static StreamingGenerationAttempt CreateAttempt(
        ILogger<StreamingGenerationAttempt> logger)
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
            "minimal",
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Array.Empty<IGenerationAttachmentSource>());

        return new StreamingGenerationAttempt(
            new FailingProviderGenerationStream(),
            new GenerationUsagePriceCalculator(),
            new FixedDateTimeProvider(CompletedAtUtc),
            logger,
            request,
            model,
            GenerationProviderIds.Google,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            StartedAtUtc,
            emergencyMaxProviderResponseBytes: 1024L * 1024L);
    }

    private sealed class FailingProviderGenerationStream
        : IProviderGenerationStream
    {
        public string ContentType => "application/json";
        public ProviderGenerationSummary? Summary => null;

        public async Task CopyToAsync(
            Stream destination,
            long maximumBytes,
            CancellationToken ct)
        {
            await destination
                .WriteAsync("""{"status":"failed"}"""u8.ToArray(), ct)
                .ConfigureAwait(false);

            throw new ImageGenerationProviderException(
                ImageGenerationProviderFailureKind.InvalidResponse,
                "The generation provider returned malformed JSON.");
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
}
